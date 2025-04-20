using System;

namespace Flow.Launcher.Infrastructure.QuickSwitch.Interface
{
    internal interface IQuickSwitchDialogTab : IDisposable
    {
        internal string GetCurrentFolder();

        internal string GetCurrentFile();

        internal bool OpenFolder(string path);
    }
}
