using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.Converters
{
    public class QuerySuggestionBoxConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 3)
            {
                return string.Empty;
            }
            var QueryTextBox = values[0] as TextBox;

            var queryText = (string)values[2];

            if (string.IsNullOrEmpty(queryText))
                return string.Empty;

            // second prop is the current selected item result
            var val = values[1];
            if (val == null)
            {
                return string.Empty;
            }
            if (!(val is ResultViewModel))
            {
                return System.Windows.Data.Binding.DoNothing;
            }

            try
            {
                var selectedItem = (ResultViewModel)val;

                var selectedResult = selectedItem.Result;
                var selectedResultActionKeyword = string.IsNullOrEmpty(selectedResult.ActionKeywordAssigned) ? "" : selectedResult.ActionKeywordAssigned + " ";
                var selectedResultPossibleSuggestion = selectedResultActionKeyword + selectedResult.Title;

                if (!selectedResultPossibleSuggestion.StartsWith(queryText, StringComparison.CurrentCultureIgnoreCase))
                    return string.Empty;


                // For AutocompleteQueryCommand.
                // When user typed lower case and result title is uppercase, we still want to display suggestion
                selectedItem.QuerySuggestionText = queryText + selectedResultPossibleSuggestion.Substring(queryText.Length);

                // Check if Text will be larger then our QueryTextBox
                System.Windows.Media.Typeface typeface = new Typeface(QueryTextBox.FontFamily, QueryTextBox.FontStyle, QueryTextBox.FontWeight, QueryTextBox.FontStretch);
                // TODO: Obsolete warning?
                System.Windows.Media.FormattedText ft = new FormattedText(QueryTextBox.Text, System.Globalization.CultureInfo.DefaultThreadCurrentCulture, System.Windows.FlowDirection.LeftToRight, typeface, QueryTextBox.FontSize, Brushes.Black);

                var offset = QueryTextBox.Padding.Right;

                if ((ft.Width + offset) > QueryTextBox.ActualWidth || QueryTextBox.HorizontalOffset != 0)
                {
                    return string.Empty;
                };

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
}
