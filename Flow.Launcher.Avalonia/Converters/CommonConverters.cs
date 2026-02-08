using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Flow.Launcher.Avalonia.Converters;

/// <summary>
/// Converts text with highlight indices to InlineCollection with bold highlights.
/// Usage: MultiBinding with [0]=text string, [1]=List&lt;int&gt; of character indices to highlight.
/// </summary>
public class HighlightTextConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 1 || values[0] is not string text || string.IsNullOrEmpty(text))
            return new InlineCollection { new Run(string.Empty) };

        // If no highlight data, return plain text as single Run
        if (values.Count < 2 || values[1] is not IList<int> { Count: > 0 } highlightData)
            return new InlineCollection { new Run(text) };

        var inlines = new InlineCollection();
        var highlightSet = new HashSet<int>(highlightData);

        // Build runs by grouping consecutive characters with same highlight state
        var currentRun = new System.Text.StringBuilder();
        var currentIsHighlight = highlightSet.Contains(0);

        for (var i = 0; i < text.Length; i++)
        {
            var shouldHighlight = highlightSet.Contains(i);

            if (shouldHighlight != currentIsHighlight && currentRun.Length > 0)
            {
                // Flush current run
                inlines.Add(CreateRun(currentRun.ToString(), currentIsHighlight));
                currentRun.Clear();
                currentIsHighlight = shouldHighlight;
            }

            currentRun.Append(text[i]);
        }

        // Flush final run
        if (currentRun.Length > 0)
            inlines.Add(CreateRun(currentRun.ToString(), currentIsHighlight));

        return inlines;
    }

    private static Run CreateRun(string text, bool isHighlight)
    {
        var run = new Run(text);
        if (isHighlight)
        {
            run.FontWeight = FontWeight.Bold;
            // Try to get from resources, fallback to gold
            if (Application.Current != null && Application.Current.TryGetResource("HighlightForegroundBrush", null, out var brush) && brush is IBrush b)
            {
                run.Foreground = b;
            }
            else
            {
                run.Foreground = new SolidColorBrush(Colors.Gold);
            }
        }
        return run;
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
