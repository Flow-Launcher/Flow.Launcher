using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Flow.Launcher.Avalonia.Converters;

/// <summary>
/// Converts text with highlight ranges to formatted text with bold highlights.
/// This is a simplified version - full implementation would use Avalonia's TextDecorations.
/// </summary>
public class HighlightTextConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // For now, just return the plain text
        // Full implementation would create formatted inline text with highlights
        if (values.Count >= 1 && values[0] is string text)
        {
            return text;
        }
        return string.Empty;
    }
}

/// <summary>
/// Converts query text and selected item to suggestion text.
/// </summary>
public class QuerySuggestionBoxConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // values[0]: QueryTextBox (element)
        // values[1]: SelectedItem
        // values[2]: QueryText

        if (values.Count < 3)
            return string.Empty;

        var queryText = values[2] as string ?? string.Empty;
        
        // For now, return empty - full implementation would show autocomplete suggestion
        // based on the selected result's title
        return string.Empty;
    }
}

/// <summary>
/// Converts integer index to ordinal number for hotkey display (1, 2, 3...).
/// </summary>
public class OrdinalConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index)
        {
            // Convert 0-based index to 1-based display, wrapping 9 to 0
            var displayNumber = (index + 1) % 10;
            return displayNumber.ToString();
        }
        return "0";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a size to a ratio of itself.
/// </summary>
public class SizeRatioConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double size && parameter is string ratioStr && double.TryParse(ratioStr, out var ratio))
        {
            return size * ratio;
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts string to null if empty (for image sources).
/// </summary>
public class StringToNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str && string.IsNullOrWhiteSpace(str))
        {
            return null;
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() ?? string.Empty;
    }
}
