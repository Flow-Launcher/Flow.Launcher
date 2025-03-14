using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher
{
    public partial class ResultListBox
    {
        protected object _lock = new object();
        private Point _lastpos;
        private ListBoxItem curItem = null;
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
                curItem = (ListBoxItem)sender;
                var p = e.GetPosition((IInputElement)sender);
                _lastpos = p;
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
                if (curItem != null)
                {
                    curItem.IsSelected = true;
                }
            }
        }


        private Point start;
        private string path;
        private string query;
        // this method is called by the UI thread, which is single threaded, so we can be sloppy with locking
        private bool isDragging;

        private void ResultList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Mouse.DirectlyOver is not FrameworkElement
                {
                    DataContext: ResultViewModel
                    {
                        Result:
                        {
                            CopyText: { } copyText,
                            OriginQuery.RawQuery: { } rawQuery
                        }
                    }
                }) return;

            path = copyText;
            query = rawQuery;
            start = e.GetPosition(null);
            isDragging = true;
        }
        private void ResultList_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || !isDragging)
            {
                start = default;
                path = string.Empty;
                query = string.Empty;
                isDragging = false;
                return;
            }

            if (!File.Exists(path) && !Directory.Exists(path))
                return;

            Point mousePosition = e.GetPosition(null);
            Vector diff = this.start - mousePosition;

            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance
                || Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            isDragging = false;

            App.API.HideMainWindow();

            var data = new DataObject(DataFormats.FileDrop, new[]
            {
                path
            });

            // Reassigning query to a new variable because for some reason
            // after DragDrop.DoDragDrop call, 'query' loses its content, i.e. becomes empty string
            var rawQuery = query;
            var effect = DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move | DragDropEffects.Copy);
            if (effect == DragDropEffects.Move)
                App.API.ChangeQuery(rawQuery, true);
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
