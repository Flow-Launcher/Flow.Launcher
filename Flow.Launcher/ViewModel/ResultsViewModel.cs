using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
            Results.RemoveAll();
        }

        public void KeepResultsFor(PluginMetadata metadata)
        {
            Results.Update(Results.Where(r => r.Result.PluginID == metadata.ID).ToList());
        }

        public void KeepResultsExcept(PluginMetadata metadata)
        {
            Results.Update(Results.Where(r => r.Result.PluginID != metadata.ID).ToList());
        }


        /// <summary>
        /// To avoid deadlock, this method should not called from main thread
        /// </summary>
        public void AddResults(List<Result> newRawResults, string resultId)
        {
            var newResults = NewResults(newRawResults, resultId);

            lock (_collectionLock)
            {
                // https://social.msdn.microsoft.com/Forums/vstudio/en-US/5ff71969-f183-4744-909d-50f7cd414954/binding-a-tabcontrols-selectedindex-not-working?forum=wpf
                // fix selected index flow
                if (Results.Count > 0)
                    SelectedIndex = 0;

                // update UI in one run, so it can avoid UI flickering
                Results.Update(newResults);
            }

            if (Visbility != Visibility.Visible && Results.Count > 0)
            {
                Margin = new Thickness { Top = 8 };
                SelectedIndex = 0;
                Visbility = Visibility.Visible;
            }
            else
            {
                Margin = new Thickness { Top = 0 };
                Visbility = Visibility.Collapsed;
            }
        }
        /// <summary>
        /// To avoid deadlock, this method should not called from main thread
        /// </summary>
        public void AddResults(IEnumerable<ResultsForUpdate> resultsForUpdates)
        {
            var newResults = NewResults(resultsForUpdates);

            lock (_collectionLock)
            {
                // https://social.msdn.microsoft.com/Forums/vstudio/en-US/5ff71969-f183-4744-909d-50f7cd414954/binding-a-tabcontrols-selectedindex-not-working?forum=wpf
                // fix selected index flow
                SelectedIndex = 0;

                Results.Update(newResults);
            }

            switch (Visbility)
            {
                case Visibility.Collapsed when Results.Count > 0:
                    Margin = new Thickness { Top = 8 };
                    SelectedIndex = 0;
                    Visbility = Visibility.Visible;
                    break;
                case Visibility.Visible when Results.Count == 0:
                    Margin = new Thickness { Top = 0 };
                    Visbility = Visibility.Collapsed;
                    break;
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
                .ToList();
        }

        private List<ResultViewModel> NewResults(IEnumerable<ResultsForUpdate> resultsForUpdates)
        {
            if (!resultsForUpdates.Any())
                return Results.ToList();

            var results = Results as IEnumerable<ResultViewModel>;

            return results.Where(r => r != null && !resultsForUpdates.Any(u => u.Metadata.ID == r.Result.PluginID))
                          .Concat(
                               resultsForUpdates.SelectMany(u => u.Results, (u, r) => new ResultViewModel(r, _settings)))
                          .OrderByDescending(rv => rv.Result.Score)
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

        public class ResultCollection : ObservableCollection<ResultViewModel>, INotifyCollectionChanged
        {
            private bool _suppressNotification = false;
            protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
            {
                if (!_suppressNotification)
                    base.OnCollectionChanged(e);
            }

            public override event NotifyCollectionChangedEventHandler CollectionChanged;

            // https://peteohanlon.wordpress.com/2008/10/22/bulk-loading-in-observablecollection/
            public void AddRange(IEnumerable<ResultViewModel> Items)
            {
                _suppressNotification = true;

                foreach (var item in Items)
                {
                    Add(item);
                }


                // wpf use directx / double buffered already, so just reset all won't cause ui flickering
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                return;

            }
            public void RemoveAll()
            {
                ClearItems();
            }

            /// <summary>
            /// Update the results collection with new results, try to keep identical results
            /// </summary>
            /// <param name="newItems"></param>
            public void Update(List<ResultViewModel> newItems)
            {
                Clear();

                AddRange(newItems);
            }
        }
    }
}