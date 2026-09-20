using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Flow.Launcher.Helper;

internal static class MouseWheelHelper
{
    /// <summary>
    /// Stops the wheel event at a nested control and raises an equivalent event on the specified scroll viewer.
    /// </summary>
    /// <param name="e">The wheel event raised by the nested control.</param>
    /// <param name="target">The scroll viewer that should process the forwarded event.</param>
    /// <param name="source">The source to preserve on the forwarded event.</param>
    public static void ForwardMouseWheelToScrollViewer(MouseWheelEventArgs e, ScrollViewer target, object source)
    {
        // The nested control must not consume the event before the parent scroll viewer can process it.
        e.Handled = true;

        target.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = source
        });
    }
}