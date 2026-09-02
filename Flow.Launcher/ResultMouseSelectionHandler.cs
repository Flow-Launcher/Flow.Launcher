using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher
{
    internal static class SelectionSurfaceNames
    {
        public const string Results = "Results";
        public const string PinnedResults = "PinnedResults";
    }

    internal sealed class ResultMouseSelectionHandler
    {
        private readonly Lock _lock = new();
        private readonly string _selectionSurface;
        private Point _lastPosition;
        private ListBoxItem _currentItem;

        public ResultMouseSelectionHandler(string selectionSurface)
        {
            _selectionSurface = selectionSurface;
        }

        public void OnMouseEnter(object sender, MouseEventArgs e)
        {
            lock (_lock)
            {
                _currentItem = (ListBoxItem)sender;
                _lastPosition = e.GetPosition((IInputElement)sender);
            }
        }

        public void OnMouseMove(object sender, MouseEventArgs e, ICommand mouseSelectCommand)
        {
            lock (_lock)
            {
                var position = e.GetPosition((IInputElement)sender);
                if (_lastPosition == position)
                {
                    return;
                }

                _lastPosition = position;
                ((ListBoxItem)sender).IsSelected = true;
                mouseSelectCommand?.Execute(_selectionSurface);
            }
        }

        public void OnPreviewMouseDown(ICommand mouseSelectCommand)
        {
            lock (_lock)
            {
                if (_currentItem == null)
                {
                    return;
                }

                _currentItem.IsSelected = true;
                mouseSelectCommand?.Execute(_selectionSurface);
            }
        }

        public static ResultViewModel ResultUnderMouse()
            => Mouse.DirectlyOver is FrameworkElement { DataContext: ResultViewModel result } ? result : null;
    }
}
