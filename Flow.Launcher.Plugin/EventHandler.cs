using System;
using System.Windows;
using System.Windows.Input;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Delegate for key down event
    /// </summary>
    /// <param name="e"></param>
    public delegate void FlowLauncherKeyDownEventHandler(FlowLauncherKeyDownEventArgs e);

    /// <summary>
    /// Delegate for query event
    /// </summary>
    /// <param name="e"></param>
    public delegate void AfterFlowLauncherQueryEventHandler(FlowLauncherQueryEventArgs e);

    /// <summary>
    /// Delegate for drop events [unused?]
    /// </summary>
    /// <param name="result"></param>
    /// <param name="dropObject"></param>
    /// <param name="e"></param>
    public delegate void ResultItemDropEventHandler(Result result, IDataObject dropObject, DragEventArgs e);

    /// <summary>
    /// Global keyboard events
    /// </summary>
    /// <param name="keyevent">WM_KEYDOWN = 256,WM_KEYUP = 257,WM_SYSKEYUP = 261,WM_SYSKEYDOWN = 260</param>
    /// <param name="vkcode"></param>
    /// <param name="state"></param>
    /// <returns>return true to continue handling, return false to intercept system handling</returns>
    public delegate bool FlowLauncherGlobalKeyboardEventHandler(int keyevent, int vkcode, SpecialKeyState state);

    /// <summary>
    /// A delegate for when the visibility is changed
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    public delegate void VisibilityChangedEventHandler(object sender, VisibilityChangedEventArgs args);
    
    /// <summary>
    /// The event args for <see cref="VisibilityChangedEventHandler"/>
    /// </summary>
    public class VisibilityChangedEventArgs : EventArgs
    {
        /// <summary>
        /// <see langword="true"/> if the main window has become visible
        /// </summary>
        public bool IsVisible { get; init; }
    }

    /// <summary>
    /// Arguments container for the Key Down event
    /// </summary>
    public class FlowLauncherKeyDownEventArgs
    {
        /// <summary>
        /// The actual query
        /// </summary>
        public string Query { get; set; }

        /// <summary>
        /// Relevant key events for this event
        /// </summary>
        public KeyEventArgs keyEventArgs { get; set; }
    }

    /// <summary>
    /// Arguments container for the Query event
    /// </summary>
    public class FlowLauncherQueryEventArgs
    {
        /// <summary>
        /// The actual query
        /// </summary>
        public Query Query { get; set; }
    }
}
