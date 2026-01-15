using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Flow.Launcher.Avalonia.Resource;

/// <summary>
/// Converter to translate a key to its localized string in XAML bindings.
/// Usage: Text="{Binding Key, Converter={StaticResource TranslationConverter}}"
/// Or simpler: Use the Translator.GetString(key) helper from code-behind
/// </summary>
public class TranslationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (value is string key && !string.IsNullOrEmpty(key))
        {
            return Translator.GetString(key);
        }

        return parameter?.ToString() ?? "[No Translation]";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotSupportedException();
    }
}
