using System;
using Windows.Win32.Foundation;

namespace Flow.Launcher.Infrastructure.QuickSwitch.Interface
{
    /// <summary>
    /// Interface for handling Windows Explorer instances in QuickSwitch.
    /// </summary>
    /// <remarks>
    /// Add models in QuickSwitch/Models folder and implement this interface.
    /// Then add the instance in QuickSwitch._quickSwitchExplorers.
    /// </remarks>
    internal interface IQuickSwitchExplorer : IDisposable
    {
        internal bool CheckExplorerWindow(HWND foreground);

        internal void RemoveExplorerWindow();

        internal string GetExplorerPath();
    }
}
