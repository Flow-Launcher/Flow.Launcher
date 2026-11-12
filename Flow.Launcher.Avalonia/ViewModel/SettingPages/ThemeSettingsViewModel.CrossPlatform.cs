using System;
using Flow.Launcher.Avalonia.Helper;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class ThemeSettingsViewModel
{
    // Same values as the Windows (WPF) import, read from the theme XAML with ThemeLoader.
    partial void ImportSizingFromTheme(string themePath)
    {
        try
        {
            var theme = ThemeLoader.Load(themePath);

            if (theme.StyleDouble("QueryBoxStyle", "FontSize") is { } queryFontSize)
            {
                QueryBoxFontSize = queryFontSize;
            }

            if (theme.StyleDouble("QueryBoxStyle", "Height") is { } height)
            {
                WindowHeightSize = height;
            }

            if (theme.ResourceDouble("ResultItemHeight") is { } itemHeight)
            {
                ItemHeightSize = itemHeight;
            }

            if (theme.StyleDouble("ItemTitleStyle", "FontSize") is { } resultFontSize)
            {
                ResultItemFontSize = resultFontSize;
            }

            if (theme.StyleDouble("ItemSubTitleStyle", "FontSize") is { } subResultFontSize)
            {
                ResultSubItemFontSize = subResultFontSize;
            }
        }
        catch (Exception ex)
        {
            Flow.Launcher.Infrastructure.Logger.Log.Exception(nameof(ThemeSettingsViewModel), $"Failed to import sizing from <{themePath}>", ex);
        }
    }
}
