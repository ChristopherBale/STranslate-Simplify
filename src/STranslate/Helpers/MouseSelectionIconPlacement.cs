using System.Drawing;

namespace STranslate.Helpers;

/// <summary>根据固定逻辑尺寸计算划词图标的物理边界，避免沿用系统回写的窗口尺寸。</summary>
internal static class MouseSelectionIconPlacement
{
    internal const double LogicalSize = 40;
    private const int LogicalOffset = 10;

    internal static Rectangle Calculate(Point point, Rectangle workArea, double dpiScaleX, double dpiScaleY)
    {
        var width = Math.Max(1, (int)Math.Round(LogicalSize * dpiScaleX));
        var height = Math.Max(1, (int)Math.Round(LogicalSize * dpiScaleY));
        var offsetX = (int)Math.Round(LogicalOffset * dpiScaleX);
        var offsetY = (int)Math.Round(LogicalOffset * dpiScaleY);
        var left = point.X + offsetX;
        var top = point.Y + offsetY;
        if (left + width > workArea.Right)
            left = point.X - width - offsetX;
        if (top + height > workArea.Bottom)
            top = point.Y - height - offsetY;

        // 显示拓扑切换期间工作区可能暂时小于图标，仍需避免 Clamp 上下界倒置。
        left = Math.Clamp(left, workArea.Left, Math.Max(workArea.Left, workArea.Right - width));
        top = Math.Clamp(top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));
        return new Rectangle(left, top, width, height);
    }
}
