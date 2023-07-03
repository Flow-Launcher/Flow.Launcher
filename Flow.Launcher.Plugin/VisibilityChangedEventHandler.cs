using System;

namespace Flow.Launcher.Plugin
{
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
}
