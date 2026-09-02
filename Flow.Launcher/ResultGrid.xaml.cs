using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher
{
    public partial class ResultGrid : UserControl
    {
        private readonly ResultMouseSelectionHandler _mouseSelectionHandler =
            new(SelectionSurfaceNames.PinnedResults);

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
            => _mouseSelectionHandler.OnMouseEnter(sender, e);

        private void OnMouseMove(object sender, MouseEventArgs e)
            => _mouseSelectionHandler.OnMouseMove(sender, e, MouseSelectCommand);

        private void ListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
            => _mouseSelectionHandler.OnPreviewMouseDown(MouseSelectCommand);

        private void ResultListBox_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var result = ResultMouseSelectionHandler.ResultUnderMouse();
            if (result == null)
                return;

            RightClickResultCommand?.Execute(result.Result);
        }

        private void ResultListBox_OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            var result = ResultMouseSelectionHandler.ResultUnderMouse();
            if (result == null)
                return;

            LeftClickResultCommand?.Execute(null);
        }
    }
}
