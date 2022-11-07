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
        private string file;
        private string query;

        private void ResultList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Mouse.DirectlyOver is not FrameworkElement { DataContext: ResultViewModel result })
                return;

            file = result.Result.CopyText;
            query = result.Result.OriginQuery.RawQuery;
            start = e.GetPosition(null);
        }

        private void ResultList_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                start = default;
                file = null;
                return;
            }

            Point mousePosition = e.GetPosition(null);
            Vector diff = this.start - mousePosition;

            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance
                || Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance
                || !File.Exists(file))
                return;

            var data = new DataObject(DataFormats.FileDrop, new[]
            {
                file
            });
            DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move | DragDropEffects.Copy);
            App.API.ChangeQuery(query, true);
            e.Handled = true;
        }
    }
}
