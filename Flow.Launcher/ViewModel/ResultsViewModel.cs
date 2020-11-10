using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.ViewModel
{
    public class ResultsViewModel : BaseModel
    {
        #region Private Fields

        public ResultCollection Results { get; }

        private readonly object _collectionLock = new object();
        private readonly Settings _settings;
        private int MaxResults => _settings?.MaxResultsToShow ?? 6;

        public ResultsViewModel()
        {
            Results = new ResultCollection();
            BindingOperations.EnableCollectionSynchronization(Results, _collectionLock);
        }
        public ResultsViewModel(Settings settings) : this()
        {
            _settings = settings;
            _settings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_settings.MaxResultsToShow))
                {
                    OnPropertyChanged(nameof(MaxHeight));
                }
            };
        }

        #endregion

        #region Properties

        public int MaxHeight => MaxResults * 50;

        public int SelectedIndex { get; set; }

        public ResultViewModel SelectedItem { get; set; }
        public Thickness Margin { get; set; }
        public Visibility Visbility { get; set; } = Visibility.Collapsed;

        #endregion

        #region Private Methods

        private int InsertIndexOf(int newScore, IList<ResultViewModel> list)
        {
            int index = 0;
            for (; index < list.Count; index++)
            {
                var result = list[index];
                if (newScore > result.Result.Score)
                {
                    break;
                }
            }
            return index;
        }

        private int NewIndex(int i)
        {
            var n = Results.Count;
            if (n > 0)
            {
                i = (n + i) % n;
                return i;
            }
            else
            {
                // SelectedIndex returns -1 if selection is empty.
                return -1;
            }
        }


        #endregion

        #region Public Methods

        public void SelectNextResult()
        {
            SelectedIndex = NewIndex(SelectedIndex + 1);
        }

        public void SelectPrevResult()
        {
            SelectedIndex = NewIndex(SelectedIndex - 1);
        }

        public void SelectNextPage()
        {
            SelectedIndex = NewIndex(SelectedIndex + MaxResults);
        }

        public void SelectPrevPage()
        {
            SelectedIndex = NewIndex(SelectedIndex - MaxResults);
        }

        public void SelectFirstResult()
        {
            SelectedIndex = NewIndex(0);
        }

        public void Clear()
        {
            Results.Clear();
        }

        public void RemoveResultsExcept(PluginMetadata metadata)
        {
            Results.RemoveAll(r => r.Result.PluginID != metadata.ID);
        }

        public void RemoveResultsFor(PluginMetadata metadata)
        {
            Results.RemoveAll(r => r.Result.PluginID == metadata.ID);
        }

        /// <summary>
        /// To avoid deadlock, this method should not called from main thread
        /// </summary>
        public void AddResults(List<Result> newRawResults, string resultId)
        {
            var newResults = NewResults(newRawResults, resultId);

            // update UI in one run, so it can avoid UI flickering
            Results.Update(newResults);

            if (Results.Count > 0)
            {
                Margin = new Thickness { Top = 8 };
                SelectedIndex = 0;
            }
            else
            {
                Margin = new Thickness { Top = 0 };
            }
        }

        private List<ResultViewModel> NewResults(List<Result> newRawResults, string resultId)
        {
            if (newRawResults.Count == 0)
                return Results.ToList();

            var results = Results as IEnumerable<ResultViewModel>;

            var newResults = newRawResults.Select(r => new ResultViewModel(r, _settings)).ToList();


            return results.Where(r => r.Result.PluginID != resultId)
                .Concat(newResults)
                .OrderByDescending(r => r.Result.Score)
                .Take(MaxResults * 2)
                .ToList();
        }
        #endregion

        #region FormattedText Dependency Property
        public static readonly DependencyProperty FormattedTextProperty = DependencyProperty.RegisterAttached(
            "FormattedText",
            typeof(Inline),
            typeof(ResultsViewModel),
            new PropertyMetadata(null, FormattedTextPropertyChanged));

        public static void SetFormattedText(DependencyObject textBlock, IList<int> value)
        {
            textBlock.SetValue(FormattedTextProperty, value);
        }

        public static Inline GetFormattedText(DependencyObject textBlock)
        {
            return (Inline)textBlock.GetValue(FormattedTextProperty);
        }

        private static void FormattedTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var textBlock = d as TextBlock;
            if (textBlock == null) return;

            var inline = (Inline)e.NewValue;

            textBlock.Inlines.Clear();
            if (inline == null) return;

            textBlock.Inlines.Add(inline);
        }
        #endregion

        public class ResultCollection : ObservableCollection<ResultViewModel>
        {

            public void RemoveAll(Predicate<ResultViewModel> predicate)
            {
                CheckReentrancy();

                for (int i = Count - 1; i >= 0; i--)
                {
                    if (predicate(this[i]))
                    {
                        RemoveAt(i);
                    }
                }
            }

            /// <summary>
            /// Update the results collection with new results, try to keep identical results
            /// </summary>
            /// <param name="newItems"></param>
            public void Update(List<ResultViewModel> newItems)
            {
                ClearItems();

                foreach (var item in newItems)
                {
                    Add(item);
                }

                
                
                return;

                int newCount = newItems.Count;
                int oldCount = Items.Count;
                int location = newCount > oldCount ? oldCount : newCount;

                for (int i = 0; i < location; i++)
                {
                    ResultViewModel oldResult = this[i];
                    ResultViewModel newResult = newItems[i];
                    if (!oldResult.Equals(newResult))
                    { // result is not the same update it in the current index
                        this[i] = newResult;
                    }
                    else if (oldResult.Result.Score != newResult.Result.Score)
                    {
                        this[i].Result.Score = newResult.Result.Score;
                    }
                }


                if (newCount >= oldCount)
                {
                    for (int i = oldCount; i < newCount; i++)
                    {
                        Add(newItems[i]);
                    }
                }
                else
                {
                    for (int i = oldCount - 1; i >= newCount; i--)
                    {
                        RemoveAt(i);
                    }
                }
            }
        }
    }
}