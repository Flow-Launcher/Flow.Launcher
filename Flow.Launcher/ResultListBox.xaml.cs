using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher
{
    public partial class ResultListBox
    {
        protected Lock _lock = new();
        private Point _lastpos;
        private ResultViewModel _currentResult = null;
        public ResultListBox()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty RightClickResultCommandProperty =
            DependencyProperty.Register("RightClickResultCommand", typeof(ICommand), typeof(ResultListBox), new UIPropertyMetadata(null));

        public ICommand RightClickResultCommand
        {
            get
            {
                return (ICommand)GetValue(RightClickResultCommandProperty);
            }
            set
            {
                SetValue(RightClickResultCommandProperty, value);
            }
        }

        public static readonly DependencyProperty LeftClickResultCommandProperty =
            DependencyProperty.Register("LeftClickResultCommand", typeof(ICommand), typeof(ResultListBox), new UIPropertyMetadata(null));

        public ICommand LeftClickResultCommand
        {
            get
            {
                return (ICommand)GetValue(LeftClickResultCommandProperty);
            }
            set
            {
                SetValue(LeftClickResultCommandProperty, value);
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] != null)
            {
                ScrollIntoView(e.AddedItems[0]);
            }
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            lock (_lock)
            {
                if (sender is FrameworkElement { DataContext: ResultViewModel result })
                {
                    _currentResult = result;
                    var p = e.GetPosition((IInputElement)sender);
                    _lastpos = p;
                }
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            lock (_lock)
            {
                var p = e.GetPosition((IInputElement)sender);
                if (_lastpos != p)
                {
                    ((ListBoxItem)sender).IsSelected = true;
                }
            }
        }

        private void ListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            lock (_lock)
            {
                if (_currentResult != null && sender is ListBox listBox)
                {
                    listBox.SelectedItem = _currentResult;
                }
            }
        }

        private Point _start;
        private string _path;
        private string _trimmedQuery;
        // this method is called by the UI thread, which is single threaded, so we can be sloppy with locking
        private bool _isDragging;

        private void ResultList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Mouse.DirectlyOver is not FrameworkElement
                {
                    DataContext: ResultViewModel
                    {
                        Result:
                        {
                            CopyText: { } copyText,
                            OriginQuery.TrimmedQuery: { } trimmedQuery
                        }
                    }
                }) return;

            _path = copyText;
            _trimmedQuery = trimmedQuery;
            _start = e.GetPosition(null);
            _isDragging = true;
        }

        private void ResultList_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || !_isDragging)
            {
                _start = default;
                _path = string.Empty;
                _trimmedQuery = string.Empty;
                _isDragging = false;
                return;
            }

            if (!File.Exists(_path) && !Directory.Exists(_path))
                return;

            Point mousePosition = e.GetPosition(null);
            Vector diff = _start - mousePosition;

            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance
                || Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            _isDragging = false;

            App.API.HideMainWindow();

            var data = new DataObject(DataFormats.FileDrop, new[]
            {
                _path
            });

            // Reassigning query to a new variable because for some reason
            // after DragDrop.DoDragDrop call, 'query' loses its content, i.e. becomes empty string
            var trimmedQuery = _trimmedQuery;
            var effect = DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move | DragDropEffects.Copy);
            if (effect == DragDropEffects.Move)
                App.API.ChangeQuery(trimmedQuery, true);
        }

        private void ResultListBox_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Mouse.DirectlyOver is not FrameworkElement { DataContext: ResultViewModel result })
                return;

            RightClickResultCommand?.Execute(result.Result);
        }

        private void ResultListBox_OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (Mouse.DirectlyOver is not FrameworkElement { DataContext: ResultViewModel result })
                return;

            LeftClickResultCommand?.Execute(null);
        }
    }
}
