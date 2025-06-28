using System;

#nullable enable

namespace Flow.Launcher.Plugins
{
    /// <summary>
    /// Interface for handling file dialog instances in QuickSwitch.
    /// </summary>
    public interface IQuickSwitchDialog : IDisposable
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
        IQuickSwitchDialogWindow? CheckDialogWindow(IntPtr hwnd);
    }

    public interface IQuickSwitchDialogWindow : IDisposable
    {
        IntPtr Handle { get; }

        IQuickSwitchDialogWindowTab GetCurrentTab();
    }

    public interface IQuickSwitchDialogWindowTab : IDisposable
    {
        IntPtr Handle { get; }

        string GetCurrentFolder();

        string GetCurrentFile();

        bool JumpFolder(string path, bool auto);

        bool JumpFile(string path);

        bool Open();
    }
}
