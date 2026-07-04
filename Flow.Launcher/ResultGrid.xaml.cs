using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher
{
    public partial class ResultGrid : UserControl
    {
        protected Lock _lock = new();
        private Point _lastpos;
        private ListBoxItem curItem = null;

        public ResultGrid()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty LeftClickResultCommandProperty =
            DependencyProperty.Register("LeftClickResultCommand", typeof(ICommand), typeof(ResultGrid), new UIPropertyMetadata(null));

        public ICommand LeftClickResultCommand
        {
            get => (ICommand)GetValue(LeftClickResultCommandProperty);
            set => SetValue(LeftClickResultCommandProperty, value);
        }

        public static readonly DependencyProperty RightClickResultCommandProperty =
            DependencyProperty.Register("RightClickResultCommand", typeof(ICommand), typeof(ResultGrid), new UIPropertyMetadata(null));

        public ICommand RightClickResultCommand
        {
            get => (ICommand)GetValue(RightClickResultCommandProperty);
            set => SetValue(RightClickResultCommandProperty, value);
        }

        public static readonly DependencyProperty MouseSelectCommandProperty =
            DependencyProperty.Register("MouseSelectCommand", typeof(ICommand), typeof(ResultGrid), new UIPropertyMetadata(null));

        public ICommand MouseSelectCommand
        {
            get => (ICommand)GetValue(MouseSelectCommandProperty);
            set => SetValue(MouseSelectCommandProperty, value);
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            lock (_lock)
            {
                curItem = (ListBoxItem)sender;
                var p = e.GetPosition(null);
                _lastpos = p;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            lock (_lock)
            {
                var p = e.GetPosition(null);
                if (Math.Abs(_lastpos.X - p.X) > 3 || Math.Abs(_lastpos.Y - p.Y) > 3)
                {
                    _lastpos = p;
                    MouseSelectCommand?.Execute(true);
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
                    MouseSelectCommand?.Execute(true);
                    curItem.IsSelected = true;
                }
            }
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
