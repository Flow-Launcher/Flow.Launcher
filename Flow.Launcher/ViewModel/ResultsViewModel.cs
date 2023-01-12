using System;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;

namespace Flow.Launcher.ViewModel
{
    public class ResultsViewModel : BaseModel
    {
        #region Private Fields

        private SourceCache<ResultsForUpdate, string> ResultCache { get; } = new(r => r.ID);

        private ReadOnlyObservableCollection<ResultViewModel> display;
        public ReadOnlyObservableCollection<ResultViewModel> Display => display;


        private readonly object _collectionLock = new();
        private readonly Settings _settings;
        private int MaxResults => _settings?.MaxResultsToShow ?? 6;

        private IDisposable _resultsSubscription;

        private ResultsViewModel()
        {
            _resultsSubscription = ResultCache.Connect()
                .TransformMany(list => list.Results.Select(r => new ResultViewModel(r, _settings)),
                    r => r.GetHashCode())
                .Sort(SortExpressionComparer<ResultViewModel>.Descending(r => r.Result.Score))
                .Bind(out display)
                .DisposeMany()
                .Subscribe();

            BindingOperations.EnableCollectionSynchronization(Display, _collectionLock);
        }
        public ResultsViewModel(Settings settings) : this()
        {
            _settings = settings;
            _settings.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(_settings.MaxResultsToShow))
                {
                    OnPropertyChanged(nameof(MaxHeight));
                }
            };
        }

        #endregion

        #region Properties

        public int MaxHeight => MaxResults * 52;

        public int SelectedIndex { get; set; }

        public ResultViewModel SelectedItem { get; set; }
        public Visibility Visibility { get; set; } = Visibility.Visible;

        public ICommand RightClickResultCommand { get; init; }
        public ICommand LeftClickResultCommand { get; init; }

        #endregion

        #region Private Methods

        private int NewIndex(int i)
        {
            var n = Display.Count;
            if (n > 0)
            {
                i = (n + i) % n;
                return i;
            }
            // SelectedIndex returns -1 if selection is empty.
            return -1;
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
            lock (_collectionLock)
                ResultCache.Clear();
        }

        /// <summary>
        /// To avoid deadlock, this method should not called from main thread
        /// </summary>
        public void AddResults(List<Result> newRawResults, string resultId)
        {
            lock (_collectionLock)
            {
                ResultCache.Edit(list =>
                {
                    list.AddOrUpdate(new ResultsForUpdate(newRawResults, new PluginMetadata()
                    {
                        ID = resultId
                    }, default));
                });
            }
        }

        /// <summary>
        /// To avoid deadlock, this method should not called from main thread
        /// </summary>
        public void AddResults(ICollection<ResultsForUpdate> resultsForUpdates, CancellationToken token)
        {
            lock (_collectionLock)
            {
                // update UI in one run, so it can avoid UI flickering
                ResultCache.Edit(list =>
                {
                    list.AddOrUpdate(resultsForUpdates);
                });
                if (display.Any())
                    SelectedItem = display[0];
            }

            // UpdateVisibility();
        }
        private void UpdateVisibility()
        {

            switch (Visibility)
            {
                case Visibility.Collapsed when Display.Count > 0:
                    SelectedIndex = 0;
                    Visibility = Visibility.Visible;
                    break;
                case Visibility.Visible when Display.Count == 0:
                    Visibility = Visibility.Collapsed;
                    break;
            }
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

    }
}
