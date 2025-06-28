using System;

#nullable enable

namespace Flow.Launcher.Plugins
{
    /// <summary>
    /// Interface for handling file explorer instances in QuickSwitch.
    /// </summary>
    public interface IQuickSwitchExplorer : IDisposable
    {
        /// <summary>
        /// Check if the foreground window is a Windows Explorer instance.
        /// </summary>
        /// <param name="hwnd">
        /// The handle of the foreground window to check.
        /// </param>
        /// <returns>
        /// The explorer window if the foreground window is a Windows Explorer instance. Null if it is not.
        /// </returns>
        IQuickSwitchExplorerWindow? CheckExplorerWindow(IntPtr hwnd);
    }

    public interface IQuickSwitchExplorerWindow : IDisposable
    {
        IntPtr Handle { get; }

        string GetExplorerPath();
    }
}
