using STranslate.Helpers;
using STranslate.Services;
using STranslate.Views;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace STranslate.Tests;

public class MouseSelectionIconWindowTests
{
    [Fact]
    public void ShowingAgainRestoresPhysicalSizeAfterNativeWindowResize()
    {
        RunOnStaThread(() =>
        {
            var window = new MouseSelectionIconWindow();
            try
            {
                var workArea = MonitorInfo.GetPrimaryDisplayMonitor().WorkingArea;
                var point = new Point(workArea.Left + workArea.Width / 2, workArea.Top + workArea.Height / 2);
                foreach (var size in new[] { 20, 1, 80, 32 })
                {
                    Win32Helper.SetWindowPhysicalBounds(window, (int)point.X, (int)point.Y, size, size, showWindow: false);
                    window.ShowAt(point);
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

                    var hwnd = new HWND(new WindowInteropHelper(window).Handle);
                    Assert.True(PInvoke.GetWindowRect(hwnd, out var bounds));
                    var dpi = VisualTreeHelper.GetDpi(window);
                    Assert.Equal((int)Math.Round(40 * dpi.DpiScaleX), bounds.right - bounds.left);
                    Assert.Equal((int)Math.Round(40 * dpi.DpiScaleY), bounds.bottom - bounds.top);
                    window.HideWindow();
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void DisplayChangesRecreateWindowAndPreserveTranslateSubscription()
    {
        RunOnStaThread(() =>
        {
            using var controller = new MouseSelectionIconController();
            var translations = 0;
            controller.TranslateRequested += (_, _) => translations++;
            var previous = controller.GetOrCreateWindow();
            for (var cycle = 0; cycle < 10; cycle++)
            {
                Assert.Same(previous, controller.GetOrCreateWindow());
                var closed = false;
                previous.Closed += (_, _) => closed = true;
                controller.InvalidateWindow();
                controller.InvalidateWindow();
                var current = controller.GetOrCreateWindow();
                Assert.True(closed);
                Assert.NotSame(previous, current);

                // 排队的旧恢复回调不能关闭已经按新显示状态创建的窗口。
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                Assert.Same(current, controller.GetOrCreateWindow());
                current.TranslateBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(cycle + 1, translations);
                previous = current;
            }
        });
    }

    [Fact]
    public void DisposingControllerClosesWindowAndRejectsFurtherCreation()
    {
        RunOnStaThread(() =>
        {
            var controller = new MouseSelectionIconController();
            var window = controller.GetOrCreateWindow();
            var closed = false;
            window.Closed += (_, _) => closed = true;
            controller.InvalidateWindow();
            controller.Dispose();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

            Assert.True(closed);
            Assert.Throws<ObjectDisposedException>(() => controller.GetOrCreateWindow());
            controller.Dispose();
        });
    }

    [Fact]
    public void IconContentStartsHiddenWhileWindowRemainsOpaque()
    {
        RunOnStaThread(() =>
        {
            var window = new MouseSelectionIconWindow();
            Assert.Equal(1d, window.Opacity);
            Assert.Equal(0d, window.IconRoot.Opacity);
        });
    }

    [Fact]
    public void ShowAnimationTargetsIconContentInsteadOfWindow()
    {
        RunOnStaThread(() =>
        {
            var window = new MouseSelectionIconWindow();
            window.StartShowAnimation();

            Assert.False(window.HasAnimatedProperties);
            Assert.True(window.IconRoot.HasAnimatedProperties);
            Assert.Equal(0d, window.IconRoot.GetAnimationBaseValue(UIElement.OpacityProperty));
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
