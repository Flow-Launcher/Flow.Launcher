using System;
using System.Linq;
using WpfFrameworkElement = System.Windows.FrameworkElement;
using WpfResourceDictionary = System.Windows.ResourceDictionary;
using WpfSetter = System.Windows.Setter;
using WpfStyle = System.Windows.Style;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class ThemeSettingsViewModel
{
    partial void ImportSizingFromTheme(string themePath)
    {
        try
        {
            var resourceDictionary = new WpfResourceDictionary
            {
                Source = new Uri(themePath, UriKind.Absolute)
            };

            if (resourceDictionary["QueryBoxStyle"] is WpfStyle queryBoxStyle)
            {
                if (TryGetSetterValue<double>(queryBoxStyle, WpfTextBox.FontSizeProperty, out var fontSize))
                {
                    QueryBoxFontSize = fontSize;
                }

                if (TryGetSetterValue<double>(queryBoxStyle, WpfFrameworkElement.HeightProperty, out var height))
                {
                    WindowHeightSize = height;
                }
            }

            if (resourceDictionary["ResultItemHeight"] is double itemHeight)
            {
                ItemHeightSize = itemHeight;
            }

            if (resourceDictionary["ItemTitleStyle"] is WpfStyle itemTitleStyle &&
                TryGetSetterValue<double>(itemTitleStyle, WpfTextBlock.FontSizeProperty, out var resultFontSize))
            {
                ResultItemFontSize = resultFontSize;
            }

            if (resourceDictionary["ItemSubTitleStyle"] is WpfStyle itemSubTitleStyle &&
                TryGetSetterValue<double>(itemSubTitleStyle, WpfTextBlock.FontSizeProperty, out var subResultFontSize))
            {
                ResultSubItemFontSize = subResultFontSize;
            }
        }
        catch (Exception)
        {
        }
    }

    private static bool TryGetSetterValue<T>(WpfStyle style, System.Windows.DependencyProperty property, out T value)
    {
        var setter = style.Setters
            .OfType<WpfSetter>()
            .FirstOrDefault(currentSetter => currentSetter.Property == property);

        if (setter?.Value is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default!;
        return false;
    }
}
