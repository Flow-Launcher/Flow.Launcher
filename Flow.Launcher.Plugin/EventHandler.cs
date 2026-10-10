using System;

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
    /// A delegate for when the actual application theme is changed
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    public delegate void ActualApplicationThemeChangedEventHandler(object sender, ActualApplicationThemeChangedEventArgs args);

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
    public partial class FlowLauncherKeyDownEventArgs
    {
        /// <summary>
        /// The actual query
        /// </summary>
        public string Query { get; set; }
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

    /// <summary>
    /// The event args for <see cref="ActualApplicationThemeChangedEventHandler"/>
    /// </summary>
    public class ActualApplicationThemeChangedEventArgs : EventArgs
    {
        /// <summary>
        /// <see langword="true"/> if the application has changed actual theme
        /// </summary>
        public bool IsDark { get; init; }
    }
}
