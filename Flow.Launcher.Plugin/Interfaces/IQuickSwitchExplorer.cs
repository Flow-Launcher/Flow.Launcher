using System;

namespace Flow.Launcher.Plugins
{
    /// <summary>
    /// Interface for handling file explorer instances in QuickSwitch.
    /// </summary>
    public interface IQuickSwitchExplorer : IDisposable
    {
        IQuickSwitchExplorerWindow ExplorerWindow { get; }

        /// <summary>
        /// Check if the foreground window is a Windows Explorer instance.
        /// </summary>
        /// <param name="foreground">
        /// The handle of the foreground window to check.
        /// </param>
        /// <returns>
        /// True if the foreground window is a Windows Explorer instance, otherwise false.
        /// </returns>
        bool CheckExplorerWindow(IntPtr foreground);

        /// <summary>
        /// 
        /// </summary>
        void RemoveExplorerWindow();
    }

    public interface IQuickSwitchExplorerWindow : IDisposable
    {
        IntPtr Handle { get; }

        string GetExplorerPath();
    }
}
