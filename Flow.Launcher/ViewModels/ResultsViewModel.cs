using System;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using Avalonia.ReactiveUI;
using DynamicData;
using DynamicData.Binding;

namespace Flow.Launcher.ViewModel
{
    public class ResultsViewModel : BaseModel
    {
        #region Private Fields

        private ReadOnlyObservableCollection<ResultViewModel> _results;
        public ReadOnlyObservableCollection<ResultViewModel> Results => _results;

        private SourceList<Result> _resultsCache = new();

        private readonly object _collectionLock = new object();
        private readonly Settings _settings;
        private int MaxResults => _settings?.MaxResultsToShow ?? 6;

        public void MoveIndex(int diff)
        {
            if (Results.Count == 0)
            {
                SelectedIndex = -1;
                return;
            }

            SelectedIndex = Mod(SelectedIndex + diff, Results.Count);
            return;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            int Mod(int x, int m) => (x % m + m) % m;
        }

        private IDisposable _resultsSubscription;

        public ResultsViewModel()
        {
            _resultsSubscription = _resultsCache.Connect()
                .Sort(SortExpressionComparer<Result>.Descending(r => r.Score))
                .Transform(r => new ResultViewModel(r, _settings))
                .Bind(out _results)
                .Do(_ => MoveIndex(-SelectedIndex))
                .Subscribe();
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

        public double MaxHeight => MaxResults * 52;

        public int SelectedIndex { get; set; }

        public ResultViewModel SelectedItem { get; set; }
        public Thickness Margin { get; set; }
        public bool Visibility { get; set; } = true;

        public ICommand RightClickResultCommand { get; init; }
        public ICommand LeftClickResultCommand { get; init; }

        #endregion

        #region Private Methods

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
            _resultsCache.Clear();
        }

        public void KeepResultsFor(PluginMetadata metadata)
        {
            _resultsCache.Edit(list =>
            {
                list.Remove(list.Where(x => x.PluginID != metadata.ID));
            });
        }

        public void KeepResultsExcept(PluginMetadata metadata)
        {
            _resultsCache.Edit(list =>
            {
                list.Remove(list.Where(x => x.PluginID == metadata.ID));
            });
        }

        /// <summary>
        /// To avoid deadlock, this method should not called from main thread
        /// </summary>
        public void AddResults(List<Result> newRawResults, string resultId)
        {
            NewResults(newRawResults, resultId);
        }

        /// <summary>
        /// To avoid deadlock, this method should not called from main thread
        /// </summary>
        public void AddResults(IEnumerable<ResultsForUpdate> resultsForUpdates, CancellationToken token)
        {
            NewResults(resultsForUpdates);
        }


        private void NewResults(IReadOnlyCollection<Result> newRawResults, string resultId)
        {
            _resultsCache.Edit(list =>
            {
                list.Remove(list.Where(x => x.PluginID == resultId).ToList());
                list.AddRange(newRawResults);
            });
        }

        private void NewResults(IEnumerable<ResultsForUpdate> resultsForUpdates)
        {
            _resultsCache.Edit(list =>
            {
                list.Remove(list.Where(x => resultsForUpdates.Any(r => r.ID == x.PluginID)).ToList());

                foreach (var resultsForUpdate in resultsForUpdates)
                {
                    list.AddRange(resultsForUpdate.Results);
                }
            });
        }

        #endregion

        #region FormattedText Dependency Property

        // public static readonly DependencyProperty FormattedTextProperty = DependencyProperty.RegisterAttached(
        //     "FormattedText",
        //     typeof(Inline),
        //     typeof(ResultsViewModel),
        //     new PropertyMetadata(null, FormattedTextPropertyChanged));
        //
        // public static void SetFormattedText(DependencyObject textBlock, IList<int> value)
        // {
        //     textBlock.SetValue(FormattedTextProperty, value);
        // }

        // public static Inline GetFormattedText(DependencyObject textBlock)
        // {
        //     return (Inline)textBlock.GetValue(FormattedTextProperty);
        // }

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
    }
}
