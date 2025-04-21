using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.ViewModel
{
    public class ResultsViewModel : BaseModel
    {
        #region Private Fields

        public ResultCollection Results { get; }

        private readonly object _collectionLock = new();
        private readonly Settings _settings;
        private int MaxResults => _settings?.MaxResultsToShow ?? 6;
        
        private ResultViewModel _lastSelectedItem;
        private int _lastSelectedIndex = -1;

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
                switch (e.PropertyName)
                {
                    case nameof(_settings.MaxResultsToShow):
                        OnPropertyChanged(nameof(MaxHeight));
                        break;
                    case nameof(_settings.ItemHeightSize):
                        OnPropertyChanged(nameof(ItemHeightSize));
                        OnPropertyChanged(nameof(MaxHeight));
                        break;
                }
            };
        }

        #endregion

        #region Properties

        public bool IsPreviewOn { get; set; }

        public double MaxHeight
        {
            get
            {
                var newResultsCount = MaxResults;
                if (IsPreviewOn)
                {
                    newResultsCount = (int)Math.Ceiling(380 / _settings.ItemHeightSize);
                    if (newResultsCount < MaxResults)
                    {
                        newResultsCount = MaxResults;
                    }
                }
                return newResultsCount * _settings.ItemHeightSize;
            }
        }

        public double ItemHeightSize
        {
            get => _settings.ItemHeightSize;
            set => _settings.ItemHeightSize = value;
        }

        public int SelectedIndex { get; set; }

        public ResultViewModel SelectedItem { get; set; }
        public Thickness Margin { get; set; }
        public Visibility Visibility { get; set; } = Visibility.Collapsed;

        public ICommand RightClickResultCommand { get; init; }
        public ICommand LeftClickResultCommand { get; init; }

        #endregion

        #region Private Methods

        private static int InsertIndexOf(int newScore, IList<ResultViewModel> list)
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

        public void SelectLastResult()
        {
            SelectedIndex = NewIndex(Results.Count - 1);
        }

        public void Clear()
        {
            lock (_collectionLock)
                Results.RemoveAll();
        }

        public void KeepResultsFor(PluginMetadata metadata)
        {
            lock (_collectionLock)
                Results.Update(Results.Where(r => r.Result.PluginID == metadata.ID).ToList());
        }

        public void KeepResultsExcept(PluginMetadata metadata)
        {
            lock (_collectionLock)
                Results.Update(Results.Where(r => r.Result.PluginID != metadata.ID).ToList());
        }

        public void ResetSelectedIndex()
        {
            _lastSelectedIndex = 0;
            if (Results.Any())
            {
                SelectedIndex = 0;
                SelectedItem = Results[0];
            }
        }
        
        /// <summary>
        /// To avoid deadlock, this method should not called from main thread
        /// </summary>
        public void AddResults(List<Result> newRawResults, string resultId)
        {
            var newResults = NewResults(newRawResults, resultId);

            UpdateResults(newResults);
        }
        /// <summary>
        /// To avoid deadlock, this method should not called from main thread
        /// </summary>
        public void AddResults(ICollection<ResultsForUpdate> resultsForUpdates, CancellationToken token, bool reselect = true)
        {
            if (resultsForUpdates == null || !resultsForUpdates.Any())
                return;

            // Save the currently selected item
            if (SelectedItem != null)
            {
                _lastSelectedItem = SelectedItem;
                _lastSelectedIndex = SelectedIndex;
            }

            // Generate new results
            var newResults = NewResults(resultsForUpdates);

            if (token.IsCancellationRequested)
                return;

            // Update results (includes logic for restoring selection)
            UpdateResults(newResults, reselect, token);
        }

        private void UpdateResults(List<ResultViewModel> newResults, bool reselect = true,
            CancellationToken token = default)
        {
            lock (_collectionLock)
            {
                // Update previous results and UI
                Results.Update(newResults, token);

                // Only perform selection logic if reselect is true
                if (reselect && Results.Any())
                {
                    // If a previously selected item exists and still remains in the list, reselect it
                    if (_lastSelectedItem != null && Results.Contains(_lastSelectedItem))
                    {
                        SelectedItem = _lastSelectedItem;
                        SelectedIndex = Results.IndexOf(_lastSelectedItem);
                    }
                    // If previous index is still within valid range, use that index
                    else if (_lastSelectedIndex >= 0 && _lastSelectedIndex < Results.Count)
                    {
                        SelectedIndex = _lastSelectedIndex;
                        SelectedItem = Results[SelectedIndex];
                    }
                    // If nothing else is valid, select the first item
                    else if (Results.Count > 0)
                    {
                        SelectedItem = Results[0];
                        SelectedIndex = 0;
                    }
                }
            }
            
            // If no item is selected, select the first one
            if (Results.Count > 0 && (SelectedIndex == -1 || SelectedItem == null))
            {
                SelectedIndex = 0;
                SelectedItem = Results[0];
            }

            // Visibility update - fix for related issue
            if (Results.Count > 0)
            {
                // 1. Always ensure index is valid when there are results
                if (SelectedIndex == -1 || SelectedItem == null)
                {
                    SelectedIndex = 0;
                    SelectedItem = Results[0];
                }

                // 2. Update visibility
                if (Visibility == Visibility.Collapsed)
                {
                    Visibility = Visibility.Visible;
                }
            }
            else if (Visibility == Visibility.Visible && Results.Count == 0)
            {
                Visibility = Visibility.Collapsed;
            }
        }


        private List<ResultViewModel> NewResults(List<Result> newRawResults, string resultId)
        {
            if (newRawResults.Count == 0)
                return Results;

            var newResults = newRawResults.Select(r => new ResultViewModel(r, _settings));

            return Results.Where(r => r.Result.PluginID != resultId)
                .Concat(newResults)
                .OrderByDescending(r => r.Result.Score)
                .ToList();
        }

        private List<ResultViewModel> NewResults(ICollection<ResultsForUpdate> resultsForUpdates)
        {
            if (!resultsForUpdates.Any())
                return Results;

            return Results.Where(r => r?.Result != null && resultsForUpdates.All(u => u.ID != r.Result.PluginID))
                          .Concat(resultsForUpdates.SelectMany(u => u.Results, (u, r) => new ResultViewModel(r, _settings)))
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
            if (d is not TextBlock textBlock) return;

            var inline = (Inline)e.NewValue;

            textBlock.Inlines.Clear();
            if (inline == null) return;

            textBlock.Inlines.Add(inline);
        }

        #endregion

        public class ResultCollection : List<ResultViewModel>, INotifyCollectionChanged
        {
            private long editTime = 0;

            private CancellationToken _token;

            public event NotifyCollectionChangedEventHandler CollectionChanged;

            protected void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
            {
                CollectionChanged?.Invoke(this, e);
            }

            public void BulkAddAll(List<ResultViewModel> resultViews)
            {
                AddRange(resultViews);

                // can return because the list will be cleared next time updated, which include a reset event
                if (_token.IsCancellationRequested)
                    return;

                // manually update event
                // wpf use DirectX / double buffered already, so just reset all won't cause ui flickering
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }

            private void AddAll(List<ResultViewModel> Items)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var item = Items[i];
                    if (_token.IsCancellationRequested)
                        return;
                    Add(item);
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, i));
                }
            }

            public void RemoveAll(int Capacity = 512)
            {
                Clear();
                if (this.Capacity > 8000 && Capacity < this.Capacity)
                    this.Capacity = Capacity;

                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }

            /// <summary>
            /// Update the results collection with new results, try to keep identical results
            /// </summary>
            /// <param name="newItems"></param>
            public void Update(List<ResultViewModel> newItems, CancellationToken token = default)
            {
                _token = token;
                if (Count == 0 && newItems.Count == 0 || _token.IsCancellationRequested)
                    return;

                if (editTime < 10 || newItems.Count < 30)
                {
                    if (Count != 0) RemoveAll(newItems.Count);
                    AddAll(newItems);
                    editTime++;
                    return;
                }
                else
                {
                    Clear();
                    BulkAddAll(newItems);
                    if (Capacity > 8000 && newItems.Count < 3000)
                    {
                        Capacity = newItems.Count;
                    }
                    editTime++;
                }
            }
        }
    }
}
