using System;
using Windows.Win32.Foundation;

namespace Flow.Launcher.Infrastructure.QuickSwitch.Interface
{
    /// <summary>
    /// Interface for handling Windows Explorer instances in QuickSwitch.
    /// </summary>
    /// <remarks>
    /// Add models which implement IQuickSwitchExplorer in folder QuickSwitch/Models.
    /// E.g. Models.WindowsExplorer.
    /// Then add instances in QuickSwitch._quickSwitchExplorers.
    /// </remarks>
    internal interface IQuickSwitchExplorer : IDisposable
    {
        bool CheckExplorerWindow(HWND foreground);

        void RemoveExplorerWindow();

        string GetExplorerPath();
    }
}
