using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.Converters;

public class QuerySuggestionBoxConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // values[0] is TextBox: The textbox displaying the autocomplete suggestion
        // values[1] is ResultViewModel: Currently selected item in the list
        // values[2] is string: Query text
        if (
            values.Length != 3 ||
            values[0] is not TextBox queryTextBox ||
            values[1] is null ||
            values[2] is not string queryText ||
            string.IsNullOrEmpty(queryText)
        )
            return string.Empty;

        if (values[1] is not ResultViewModel selectedItem)
            return Binding.DoNothing;

        try
        {
            var selectedResult = selectedItem.Result;
            var selectedResultActionKeyword = string.IsNullOrEmpty(selectedResult.ActionKeywordAssigned) ? "" : selectedResult.ActionKeywordAssigned + " ";
            var selectedResultPossibleSuggestion = selectedResultActionKeyword + selectedResult.Title;

            if (!selectedResultPossibleSuggestion.StartsWith(queryText, StringComparison.CurrentCultureIgnoreCase))
                return string.Empty;


            // For AutocompleteQueryCommand.
            // When user typed lower case and result title is uppercase, we still want to display suggestion
            selectedItem.QuerySuggestionText = queryText + selectedResultPossibleSuggestion.Substring(queryText.Length);

            // Check if Text will be larger than our QueryTextBox
            Typeface typeface = new Typeface(queryTextBox.FontFamily, queryTextBox.FontStyle, queryTextBox.FontWeight, queryTextBox.FontStretch);
            // TODO: Obsolete warning?
            var ft = new FormattedText(queryTextBox.Text, CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight, typeface, queryTextBox.FontSize, Brushes.Black);

            var offset = queryTextBox.Padding.Right;

            if (ft.Width + offset > queryTextBox.ActualWidth || queryTextBox.HorizontalOffset != 0)
                return string.Empty;

            return selectedItem.QuerySuggestionText;
        }
        catch (Exception e)
        {
            Log.Exception(nameof(QuerySuggestionBoxConverter), "fail to convert text for suggestion box", e);
            return string.Empty;
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
