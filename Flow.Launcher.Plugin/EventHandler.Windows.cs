using System.Windows;
using System.Windows.Input;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Delegate for drop events [unused?]
    /// </summary>
    /// <param name="result"></param>
    /// <param name="dropObject"></param>
    /// <param name="e"></param>
    public delegate void ResultItemDropEventHandler(Result result, IDataObject dropObject, DragEventArgs e);

    public partial class FlowLauncherKeyDownEventArgs
    {
        /// <summary>
        /// Relevant key events for this event
        /// </summary>
        public KeyEventArgs keyEventArgs { get; set; }
    }
}
