using System;
using Windows.Win32.Foundation;

namespace Flow.Launcher.Infrastructure.QuickSwitch.Interface
{
    internal interface IQuickSwitchDialogWindowTab : IDisposable
    {
        internal HWND Handle { get; }

        internal string GetCurrentFolder();

        internal string GetCurrentFile();

        internal bool JumpFolder(string path, bool auto);

        internal bool JumpFile(string path);

        internal bool Open();
    }
}
