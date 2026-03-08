using System.Windows.Media;

namespace Flow.Launcher.Core.Resource;

public static class ThemeHelper
{
    public static SolidColorBrush GetFreezeSolidColorBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
