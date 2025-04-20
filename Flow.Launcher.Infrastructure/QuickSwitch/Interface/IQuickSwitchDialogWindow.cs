using System;
using Windows.Win32.Foundation;

namespace Flow.Launcher.Infrastructure.QuickSwitch.Interface
{
    internal interface IQuickSwitchDialogWindow : IDisposable
    {
        HWND Handle { get; }

        IQuickSwitchDialogWindowTab GetCurrentTab();
    }
}
