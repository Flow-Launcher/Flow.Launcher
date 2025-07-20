using System;

#nullable enable

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Interface for handling file explorer instances in DialogJump.
    /// </summary>
    public interface IDialogJumpExplorer : IFeatures, IDisposable
    {
        /// <summary>
        /// Check if the foreground window is a Windows Explorer instance.
        /// </summary>
        /// <param name="hwnd">
        /// The handle of the foreground window to check.
        /// </param>
        /// <returns>
        /// The window if the foreground window is a file explorer instance. Null if it is not.
        /// </returns>
        IDialogJumpExplorerWindow? CheckExplorerWindow(IntPtr hwnd);
    }

    /// <summary>
    /// Interface for handling a specific file explorer window in DialogJump.
    /// </summary>
    public interface IDialogJumpExplorerWindow : IDisposable
    {
        /// <summary>
        /// The handle of the explorer window.
        /// </summary>
        IntPtr Handle { get; }

        /// <summary>
        /// Get the current folder path of the explorer window.
        /// </summary>
        /// <returns></returns>
        string? GetExplorerPath();
    }
}
