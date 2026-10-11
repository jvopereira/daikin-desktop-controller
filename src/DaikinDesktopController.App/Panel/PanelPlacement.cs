using System.Windows;
namespace DaikinDesktopController.App.Panel;
public static class PanelPlacement
{
    public static Point Compute(Rect workArea, Rect screenBounds, Size panelSize)
    {
        const double margin = 8;
        var left = workArea.Left > screenBounds.Left;
        var top = workArea.Top > screenBounds.Top;
        var x = left ? workArea.Left + margin : workArea.Right - panelSize.Width - margin;
        var y = top ? workArea.Top + margin : workArea.Bottom - panelSize.Height - margin;
        return new Point(Math.Clamp(x, workArea.Left, Math.Max(workArea.Left, workArea.Right - panelSize.Width)),
            Math.Clamp(y, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - panelSize.Height)));
    }
}
