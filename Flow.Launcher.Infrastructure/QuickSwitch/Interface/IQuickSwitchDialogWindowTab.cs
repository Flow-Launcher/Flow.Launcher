using System;
using Windows.Win32.Foundation;

namespace Flow.Launcher.Infrastructure.QuickSwitch.Interface
{
    internal interface IQuickSwitchDialogWindowTab : IDisposable
    {
        HWND Handle { get; }

        string GetCurrentFolder();

        string GetCurrentFile();

        bool JumpFolder(string path, bool auto);

        bool JumpFile(string path);

        bool Open();
    }
}
