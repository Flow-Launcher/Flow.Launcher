using System;

namespace Flow.Launcher.Plugins
{
    /// <summary>
    /// Interface for handling file dialog instances in QuickSwitch.
    /// </summary>
    public interface IQuickSwitchDialog : IDisposable
    {
        /// <summary>
        /// 
        /// </summary>
        IQuickSwitchDialogWindow DialogWindow { get; }

        bool CheckDialogWindow(IntPtr hwnd);
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
