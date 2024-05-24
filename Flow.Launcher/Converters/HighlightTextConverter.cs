using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;

namespace Flow.Launcher.Converters;

public class HighlightTextConverter : IMultiValueConverter
{
    public object Convert(object[] value, Type targetType, object parameter, CultureInfo cultureInfo)
    {
        if (value.Length < 2)
            return new Run(string.Empty);

        if (value[0] is not string text)
            return new Run(string.Empty);

        if (value[1] is not List<int> { Count: > 0 } highlightData)
            // No highlight data, just return the text
            return new Run(text);

        var highlightStyle = (Style)Application.Current.FindResource("HighlightStyle");
        var textBlock = new Span();

        for (var i = 0; i < text.Length; i++)
        {
            var currentCharacter = text.Substring(i, 1);
            var run = new Run(currentCharacter)
            {
                Style = ShouldHighlight(highlightData, i) ? highlightStyle : null
            };
            textBlock.Inlines.Add(run);
        }
        return textBlock;
    }

    public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
    {
        return new[] { DependencyProperty.UnsetValue, DependencyProperty.UnsetValue };
    }

    private bool ShouldHighlight(List<int> highlightData, int index)
    {
        return highlightData.Contains(index);
    }
}
