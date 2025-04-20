using System;
using Windows.Win32.Foundation;

namespace Flow.Launcher.Infrastructure.QuickSwitch.Interface
{
    /// <summary>
    /// Interface for handling File Dialog instances in QuickSwitch.
    /// </summary>
    /// <remarks>
    /// Add models in QuickSwitch/Models folder and implement this interface.
    /// Then add the instance in QuickSwitch._quickSwitchDialogs.
    /// </remarks>
    internal interface IQuickSwitchDialog : IDisposable
    {
        internal IQuickSwitchDialogWindow DialogWindow { get; }

        internal bool CheckDialogWindow(HWND hwnd);
    }
}
