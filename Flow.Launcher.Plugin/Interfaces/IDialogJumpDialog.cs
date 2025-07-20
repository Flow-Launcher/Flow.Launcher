using System;

#nullable enable

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Interface for handling file dialog instances in DialogJump.
    /// </summary>
    public interface IDialogJumpDialog : IFeatures, IDisposable
    {
        /// <summary>
        /// Check if the foreground window is a file dialog instance.
        /// </summary>
        /// <param name="hwnd">
        /// The handle of the foreground window to check.
        /// </param>
        /// <returns>
        /// The window if the foreground window is a file dialog instance. Null if it is not.
        /// </returns>
        IDialogJumpDialogWindow? CheckDialogWindow(IntPtr hwnd);
    }

    /// <summary>
    /// Interface for handling a specific file dialog window in DialogJump.
    /// </summary>
    public interface IDialogJumpDialogWindow : IDisposable
    {
        /// <summary>
        /// The handle of the dialog window.
        /// </summary>
        IntPtr Handle { get; }

        /// <summary>
        /// Get the current tab of the dialog window.
        /// </summary>
        /// <returns></returns>
        IDialogJumpDialogWindowTab GetCurrentTab();
    }

    /// <summary>
    /// Interface for handling a specific tab in a file dialog window in DialogJump.
    /// </summary>
    public interface IDialogJumpDialogWindowTab : IDisposable
    {
        /// <summary>
        /// The handle of the dialog tab.
        /// </summary>
        IntPtr Handle { get; }

        /// <summary>
        /// Get the current folder path of the dialog tab.
        /// </summary>
        /// <returns></returns>
        string GetCurrentFolder();

        /// <summary>
        /// Get the current file of the dialog tab.
        /// </summary>
        /// <returns></returns>
        string GetCurrentFile();

        /// <summary>
        /// Jump to a folder in the dialog tab.
        /// </summary>
        /// <param name="path">
        /// The path to the folder to jump to.
        /// </param>
        /// <param name="auto">
        /// Whether folder jump is under automatical mode.
        /// </param>
        /// <returns>
        /// True if the jump was successful, false otherwise.
        /// </returns>
        bool JumpFolder(string path, bool auto);

        /// <summary>
        /// Jump to a file in the dialog tab.
        /// </summary>
        /// <param name="path">
        /// The path to the file to jump to.
        /// </param>
        /// <returns>
        /// True if the jump was successful, false otherwise.
        /// </returns>
        bool JumpFile(string path);

        /// <summary>
        /// Open the file in the dialog tab.
        /// </summary>
        /// <returns>
        /// True if the file was opened successfully, false otherwise.
        /// </returns>
        bool Open();
    }
}
