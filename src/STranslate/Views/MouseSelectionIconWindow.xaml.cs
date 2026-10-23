using STranslate.Helpers;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

using DrawingPoint = System.Drawing.Point;

namespace STranslate.Views;

/// <summary>
/// 显示在鼠标划词位置附近的无激活翻译入口。
/// </summary>
public partial class MouseSelectionIconWindow : Window
{
    private const double InitialIconScale = 0.86;
    private static readonly Duration ShowAnimationDuration = TimeSpan.FromMilliseconds(140);
    private readonly DispatcherTimer _hideTimer;
    private int _showRequestId;

    /// <summary>
    /// 用户点击翻译图标时触发。
    /// </summary>
    public event EventHandler? TranslateRequested;

    /// <summary>
    /// 初始化鼠标划词悬浮图标窗口。
    /// </summary>
    public MouseSelectionIconWindow()
    {
        InitializeComponent();

        _hideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _hideTimer.Tick += (_, _) => HideWindow();

        MouseEnter += (_, _) => _hideTimer.Stop();
        MouseLeave += (_, _) => RestartHideTimer();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new HWND(new WindowInteropHelper(this).Handle);
        var extendedStyle = (WINDOW_EX_STYLE)(uint)PInvoke.GetWindowLongPtr(
            hwnd,
            WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        PInvoke.SetWindowLongPtr(
            hwnd,
            WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
            (nint)(extendedStyle | WINDOW_EX_STYLE.WS_EX_NOACTIVATE));
    }

    /// <summary>
    /// 仅显示图标，不传递文本
    /// </summary>
    /// <param name="point">鼠标的物理屏幕坐标</param>
    public void ShowAt(System.Windows.Point point)
    {
        var physicalPoint = new DrawingPoint((int)Math.Round(point.X), (int)Math.Round(point.Y));
        var dpi = Win32Helper.GetDpiScaleForPhysicalPoint(physicalPoint.X, physicalPoint.Y);

        var showRequestId = ++_showRequestId;
        StopShowAnimation();
        PrepareContentForShow();
        Win32Helper.SetWindowCloaked(this, cloaked: true);
        ApplyPhysicalBounds(physicalPoint, dpi);
        Show();
        RestartHideTimer();
        // Render 优先级本身不保证画面已经提交；Loaded 可确保透明初始帧先完成布局和渲染。
        _ = Dispatcher.InvokeAsync(
            () => RevealAndAnimate(showRequestId, physicalPoint),
            DispatcherPriority.Loaded);
    }

    internal void StartShowAnimation()
    {
        var showRequestId = _showRequestId;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var opacityAnimation = CreateShowAnimation(0, 1, easing);
        opacityAnimation.Completed += (_, _) =>
        {
            if (showRequestId == _showRequestId && IsVisible)
            {
                // 先在动画仍保持终值时更新基值，再移除时钟，避免完成瞬间跳回透明初始态。
                IconRoot.Opacity = 1;
                IconScaleTransform.ScaleX = 1;
                IconScaleTransform.ScaleY = 1;
                StopShowAnimation();
            }
        };

        IconRoot.BeginAnimation(
            OpacityProperty,
            opacityAnimation,
            HandoffBehavior.SnapshotAndReplace);
        IconScaleTransform.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            CreateShowAnimation(InitialIconScale, 1, easing),
            HandoffBehavior.SnapshotAndReplace);
        IconScaleTransform.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            CreateShowAnimation(InitialIconScale, 1, easing),
            HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>
    /// 隐藏当前悬浮图标。
    /// </summary>
    public void HideWindow()
    {
        _showRequestId++;
        _hideTimer.Stop();
        if (!IsVisible)
            return;

        Win32Helper.SetWindowCloaked(this, cloaked: true);
        StopShowAnimation();
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        _showRequestId++;
        _hideTimer.Stop();
        StopShowAnimation();
        base.OnClosed(e);
    }

    /// <summary>
    /// 判断物理屏幕坐标是否位于当前图标窗口内。
    /// </summary>
    /// <param name="point">物理屏幕坐标。</param>
    /// <returns>窗口可见且坐标位于窗口内时返回 true。</returns>
    public bool ContainsPhysicalPoint(DrawingPoint point)
    {
        if (!IsVisible)
            return false;

        var hwnd = new HWND(new WindowInteropHelper(this).Handle);
        return PInvoke.GetWindowRect(hwnd, out var bounds) &&
               point.X >= bounds.left &&
               point.X < bounds.right &&
               point.Y >= bounds.top &&
               point.Y < bounds.bottom;
    }

    private void TranslateBtn_Click(object sender, RoutedEventArgs e)
    {
        HideWindow();
        TranslateRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RestartHideTimer()
    {
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void RevealAndAnimate(int showRequestId, DrawingPoint physicalPoint)
    {
        if (showRequestId != _showRequestId || !IsVisible)
            return;

        // 跨屏定位后 HWND 才拥有目标 DPI；使用 WPF 当前 DPI 重设固定尺寸并完成布局，
        // 避免把旧 DPI 下 WM_SIZE 回写的 Width/Height 带进下一次显示。
        ApplyPhysicalBounds(physicalPoint, VisualTreeHelper.GetDpi(this));
        UpdateLayout();

        // 等 DWM 接收透明初始帧，并在 Cloak 内挂好动画时钟后再显示，避免暴露动画前后的中间基值。
        Win32Helper.FlushDesktopComposition();
        StartShowAnimation();
        Win32Helper.SetWindowCloaked(this, cloaked: false);
    }

    private void RestoreLogicalSize()
    {
        Width = MouseSelectionIconPlacement.LogicalSize;
        Height = MouseSelectionIconPlacement.LogicalSize;
    }

    private void ApplyPhysicalBounds(DrawingPoint point, DpiScale dpi)
    {
        RestoreLogicalSize();
        var monitor = MonitorInfo.GetDisplayMonitors()
            .FirstOrDefault(item => item.Bounds.Contains(new System.Windows.Point(point.X, point.Y)))
            ?? MonitorInfo.GetCursorDisplayMonitor();
        var workArea = new Rectangle(
            (int)Math.Round(monitor.WorkingArea.Left),
            (int)Math.Round(monitor.WorkingArea.Top),
            (int)Math.Round(monitor.WorkingArea.Width),
            (int)Math.Round(monitor.WorkingArea.Height));
        var bounds = MouseSelectionIconPlacement.Calculate(point, workArea, dpi.DpiScaleX, dpi.DpiScaleY);
        Win32Helper.SetWindowPhysicalBounds(this, bounds.Left, bounds.Top, bounds.Width, bounds.Height, showWindow: false);
    }

    private void PrepareContentForShow()
    {
        IconRoot.Opacity = 0;
        IconScaleTransform.ScaleX = InitialIconScale;
        IconScaleTransform.ScaleY = InitialIconScale;
    }

    private void StopShowAnimation()
    {
        IconRoot.BeginAnimation(OpacityProperty, null);
        IconScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        IconScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
    }

    private static DoubleAnimation CreateShowAnimation(double from, double to, IEasingFunction easing)
        => new(from, to, ShowAnimationDuration)
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };
}
