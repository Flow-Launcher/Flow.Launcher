using System.Windows.Input;

namespace Flow.Launcher.ViewModel
{
    public enum GridKeyAction
    {
        NotHandled,
        Consume,
        EnterGrid,
        LeaveGrid,
        NextRow,
        PrevRow,
        NextCell,
        PrevCell
    }

    public static class GridKeyRouting
    {
        public static GridKeyAction Route(Key key, ModifierKeys modifiers, bool gridHasControl)
        {
            if (key == Key.Tab && (modifiers & ~ModifierKeys.Shift) == ModifierKeys.None)
            {
                return RouteTab(modifiers == ModifierKeys.Shift, gridHasControl);
            }

            if (modifiers != ModifierKeys.None)
            {
                return GridKeyAction.NotHandled;
            }

            return (key, gridHasControl) switch
            {
                (Key.Down, false) => GridKeyAction.EnterGrid,
                (Key.Down, true) => GridKeyAction.NextRow,
                (Key.Up, false) => GridKeyAction.Consume,
                (Key.Up, true) => GridKeyAction.PrevRow,
                (Key.Right, true) => GridKeyAction.NextCell,
                (Key.Left, true) => GridKeyAction.PrevCell,
                (Key.Escape, true) => GridKeyAction.LeaveGrid,
                _ => GridKeyAction.NotHandled
            };
        }

        private static GridKeyAction RouteTab(bool shiftPressed, bool gridHasControl)
        {
            if (gridHasControl) return GridKeyAction.LeaveGrid;
            return shiftPressed ? GridKeyAction.Consume : GridKeyAction.EnterGrid;
        }
    }
}
