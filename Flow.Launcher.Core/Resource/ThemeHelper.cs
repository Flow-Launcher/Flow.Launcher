using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Flow.Launcher.Core.Resource;

public static class ThemeHelper
{
    public static void CopyStyle(Style originalStyle, Style targetStyle)
    {
        // If the style is based on another style, use the same base style for the target style
        if (originalStyle.BasedOn != null)
        {
            targetStyle.BasedOn = originalStyle.BasedOn;
        }

        // Copy the setters from the original style
        foreach (var setter in originalStyle.Setters.OfType<Setter>())
        {
            targetStyle.Setters.Add(new Setter(setter.Property, setter.Value));
        }
    }

    public static SolidColorBrush GetFrozenSolidColorBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
