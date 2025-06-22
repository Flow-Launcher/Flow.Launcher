using System;
using Windows.Win32.Foundation;

namespace Flow.Launcher.Infrastructure.QuickSwitch.Interface
{
    /// <summary>
    /// Interface for handling File Dialog instances in QuickSwitch.
    /// </summary>
    /// <remarks>
    /// Add models which implement IQuickSwitchDialog in folder QuickSwitch/Models.
    /// E.g. Models.WindowsDialog.
    /// Then add instances in QuickSwitch._quickSwitchDialogs.
    /// </remarks>
    internal interface IQuickSwitchDialog : IDisposable
    {
        string Name { get; }

        IQuickSwitchDialogWindow DialogWindow { get; }

        bool CheckDialogWindow(HWND hwnd);
    }
}
