using System;
using System.Windows;

namespace Flow.Launcher.Plugin.SharedCommands
{
    public static partial class FilesFolders
    {
        // No toolkit-independent message box exists off Windows; callers that need UI must pass messageBoxExShow.
        private static MessageBoxResult DefaultMessageBoxShow(string messageBoxText)
        {
            Console.Error.WriteLine(messageBoxText);
            return MessageBoxResult.OK;
        }
    }
}
