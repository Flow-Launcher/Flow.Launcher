using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher
{
    public partial class ResultGrid : UserControl
    {
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

        private Point _lastPos;

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (ViewModel == null) return;
            ViewModel.IsGridMode = true;
            var p = e.GetPosition((IInputElement)sender);
            _lastPos = p;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (ViewModel == null) return;
            var p = e.GetPosition((IInputElement)sender);
            if (_lastPos != p)
            {
                ViewModel.IsGridMode = true;
                ((ListBoxItem)sender).IsSelected = true;
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
