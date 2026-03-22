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

        private ResultsViewModel ViewModel => DataContext as ResultsViewModel;

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
                    MouseSelectCommand?.Execute(true);
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
                    MouseSelectCommand?.Execute(true);
                }
            }
        }

        private void OnItemClick(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel == null) return;
            if (sender is FrameworkElement element && element.DataContext is ResultViewModel resultVM)
            {
                ViewModel.SelectedItem = resultVM;
                if (LeftClickResultCommand != null && LeftClickResultCommand.CanExecute(null))
                {
                    LeftClickResultCommand.Execute(null);
                }
            }
        }
    }
}
