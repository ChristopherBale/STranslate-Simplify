using Microsoft.Win32;
using STranslate.Views;
using System.Drawing;
using System.Windows.Threading;

namespace STranslate.Services;

/// <summary>保持划词入口订阅稳定，在桌面会话或显示配置变化后重建透明窗口。</summary>
public sealed class MouseSelectionIconController : IDisposable
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private MouseSelectionIconWindow? _window;
    private int _displayVersion;
    private int _windowVersion;
    private bool _disposed;

    public MouseSelectionIconController()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public event EventHandler? TranslateRequested;

    public void ShowAt(System.Windows.Point point) => GetOrCreateWindow().ShowAt(point);

    public void HideWindow()
    {
        _dispatcher.VerifyAccess();
        _window?.HideWindow();
    }

    public bool ContainsPhysicalPoint(Point point)
    {
        _dispatcher.VerifyAccess();
        return _windowVersion == Volatile.Read(ref _displayVersion) &&
               _window?.ContainsPhysicalPoint(point) == true;
    }

    internal MouseSelectionIconWindow GetOrCreateWindow()
    {
        _dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        ResetWindowIfNeeded();
        if (_window is null)
        {
            _window = new MouseSelectionIconWindow();
            _window.TranslateRequested += OnTranslateRequested;
        }
        return _window;
    }

    private void OnTranslateRequested(object? sender, EventArgs e) => TranslateRequested?.Invoke(this, e);

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e) => InvalidateWindow();

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => InvalidateWindow();

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode is PowerModes.Suspend or PowerModes.Resume)
            InvalidateWindow();
    }

    internal void InvalidateWindow()
    {
        // 系统事件来自非 UI 线程。先同步标记失效，保证下一次显示不会抢先复用旧窗口。
        Interlocked.Increment(ref _displayVersion);
        if (!_dispatcher.HasShutdownStarted)
            _ = _dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                if (!_disposed)
                    ResetWindowIfNeeded();
            }));
    }

    private void ResetWindowIfNeeded()
    {
        var version = Volatile.Read(ref _displayVersion);
        if (_windowVersion == version)
            return;

        ReleaseWindow();
        _windowVersion = version;
    }

    private void ReleaseWindow()
    {
        var window = _window;
        _window = null;
        if (window is null)
            return;

        window.TranslateRequested -= OnTranslateRequested;
        window.Close();
    }

    public void Dispose()
    {
        _dispatcher.VerifyAccess();
        if (_disposed)
            return;

        _disposed = true;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        ReleaseWindow();
    }
}
