using System.Windows;

namespace Flow.Launcher.Plugin.SharedCommands
{
    public static partial class FilesFolders
    {
        private static MessageBoxResult DefaultMessageBoxShow(string messageBoxText) => MessageBox.Show(messageBoxText);
    }
}
