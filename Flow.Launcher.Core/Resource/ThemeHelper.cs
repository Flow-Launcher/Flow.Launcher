using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Flow.Launcher.Core.Resource;

public static class ThemeHelper
{
    public static void CopyStyle(Style originalStyle, Style targetStyle)
    {
        // If the style is based on another style, copy the base style first
        if (originalStyle.BasedOn != null)
        {
            CopyStyle(originalStyle.BasedOn, targetStyle);
        }

        // Copy the setters from the original style
        foreach (var setter in originalStyle.Setters.OfType<Setter>())
        {
            targetStyle.Setters.Add(new Setter(setter.Property, setter.Value));
        }
    }

    public static SolidColorBrush GetFreezeSolidColorBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
