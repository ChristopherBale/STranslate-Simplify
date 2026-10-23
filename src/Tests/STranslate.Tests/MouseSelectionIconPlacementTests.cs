using STranslate.Helpers;
using System.Drawing;

namespace STranslate.Tests;

public class MouseSelectionIconPlacementTests
{
    [Fact]
    public void RepeatedDpiChangesAlwaysReturnToOriginalSizeAndPosition()
    {
        var point = new Point(300, 200);
        var workArea = new Rectangle(0, 0, 1920, 1040);
        for (var cycle = 0; cycle < 50; cycle++)
        {
            foreach (var scale in new[] { 1d, 1.25, 1.5, 2, 1.5, 1d })
            {
                var bounds = MouseSelectionIconPlacement.Calculate(point, workArea, scale, scale);
                Assert.Equal((int)Math.Round(40 * scale), bounds.Width);
                Assert.Equal((int)Math.Round(40 * scale), bounds.Height);
                Assert.True(workArea.Contains(bounds));
            }
            Assert.Equal(new Rectangle(310, 210, 40, 40),
                MouseSelectionIconPlacement.Calculate(point, workArea, 1, 1));
        }
    }

    [Fact]
    public void HighDpiIconFlipsAwayFromBottomRightEdgeOnNegativeMonitor()
    {
        var workArea = new Rectangle(-2560, -1440, 2560, 1400);
        var bounds = MouseSelectionIconPlacement.Calculate(new Point(-5, -45), workArea, 1.5, 1.5);

        Assert.Equal(new Rectangle(-80, -120, 60, 60), bounds);
        Assert.True(workArea.Contains(bounds));
    }

    [Fact]
    public void TemporaryTinyWorkAreaDoesNotThrowOrShrinkIcon()
    {
        var bounds = MouseSelectionIconPlacement.Calculate(new Point(0, 0), new Rectangle(0, 0, 1, 1), 2, 2);

        Assert.Equal(new Rectangle(0, 0, 80, 80), bounds);
    }
}
