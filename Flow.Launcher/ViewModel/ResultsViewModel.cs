﻿using System;
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

        private readonly string ClassName = nameof(ResultsViewModel);

        public ResultCollection Results { get; }

        private readonly object _collectionLock = new();
        private readonly Settings _settings;
        private readonly MainViewModel _mainVM;
        private int MaxResults => _settings?.MaxResultsToShow ?? 6;

        public ResultsViewModel()
        {
            Results = new ResultCollection();
            BindingOperations.EnableCollectionSynchronization(Results, _collectionLock);
        }

        public ResultsViewModel(Settings settings, MainViewModel mainVM) : this()
        {
            _settings = settings;
            _mainVM = mainVM;
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
            // Since NewResults may need to clear existing results, do not check token cancellation after this point
            var newResults = NewResults(resultsForUpdates);

            UpdateResults(newResults, reselect, token);
        }

        private void UpdateResults(List<ResultViewModel> newResults, bool reselect = true, CancellationToken token = default)
        {
            lock (_collectionLock)
            {
                // update UI in one run, so it can avoid UI flickering
                Results.Update(newResults, token);
                if (reselect && Results.Any())
                    SelectedItem = Results[0];
            }

            if (token.IsCancellationRequested)
                return;

            switch (Visibility)
            {
                case Visibility.Collapsed when Results.Count > 0:
                    if (_mainVM == null || // The results are for preview only in appearance page
                        _mainVM.ResultsSelected(this)) // The results are selected
                    {
                        SelectedIndex = 0;
                        Visibility = Visibility.Visible;
                    }
                    break;
                case Visibility.Visible when Results.Count == 0:
                    Visibility = Visibility.Collapsed;
                    break;
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
            {
                App.API.LogDebug(ClassName, "No results for updates, returning existing results");
                return Results;
            }

            var newResults = resultsForUpdates.SelectMany(u => u.Results, (u, r) => new ResultViewModel(r, _settings));

            if (resultsForUpdates.Any(x => x.ShouldClearExistingResults))
            {
                App.API.LogDebug(ClassName, $"Existing results are cleared for query");
                return newResults.OrderByDescending(rv => rv.Result.Score).ToList();
            }

            App.API.LogDebug(ClassName, $"Keeping existing results for {resultsForUpdates.Count} queries");
            return Results.Where(r => r?.Result != null && resultsForUpdates.All(u => u.ID != r.Result.PluginID))
                              .Concat(newResults)
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

            public event NotifyCollectionChangedEventHandler CollectionChanged;

            protected void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
            {
                CollectionChanged?.Invoke(this, e);
            }

            private void BulkAddAll(List<ResultViewModel> resultViews, CancellationToken token = default)
            {
                AddRange(resultViews);

                // can return because the list will be cleared next time updated, which include a reset event
                if (token.IsCancellationRequested)
                    return;

                // manually update event
                // wpf use DirectX / double buffered already, so just reset all won't cause ui flickering
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }

            private void AddAll(List<ResultViewModel> Items, CancellationToken token = default)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var item = Items[i];
                    if (token.IsCancellationRequested)
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
                // Since NewResults may need to clear existing results, so we cannot check token cancellation here
                if (Count == 0 && newItems.Count == 0)
                    return;

                if (editTime < 10 || newItems.Count < 30)
                {
                    if (Count != 0) RemoveAll(newItems.Count);

                    // After results are removed, we need to check the token cancellation
                    // so that we will not add new items from the cancelled queries
                    if (token.IsCancellationRequested) return;

                    AddAll(newItems, token);
                    editTime++;
                }
                else
                {
                    Clear();

                    // After results are removed, we need to check the token cancellation
                    // so that we will not add new items from the cancelled queries
                    if (token.IsCancellationRequested) return;

                    BulkAddAll(newItems, token);
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
