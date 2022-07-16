using System;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;

namespace Flow.Launcher.Helper
{
    public class FileExplorerHelper
    {

        /// <summary>
        /// Gets the path of the file explorer that is currently in the foreground
        /// </summary>
        public static string GetActiveExplorerPath()
        {
            var explorerWindow = GetActiveExplorer();
            string locationUrl = explorerWindow.LocationURL;
            if (!string.IsNullOrEmpty(locationUrl))
            {
                return new Uri(locationUrl).LocalPath;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the file explorer that is currently in the foreground
        /// </summary>
        private static SHDocVw.InternetExplorer GetActiveExplorer()
        {
            // get the active window
            IntPtr handle = GetForegroundWindow();

            // Required ref: SHDocVw (Microsoft Internet Controls COM Object) - C:\Windows\system32\ShDocVw.dll
            var shellWindows = new SHDocVw.ShellWindows();

            // loop through all windows
            foreach (var window in shellWindows)
            {
                if (window is SHDocVw.InternetExplorer explorerWindow && new IntPtr(explorerWindow.HWND) == handle)
                {
                    // we have found the desired window, now let's make sure that it is indeed a file explorer
                    // we don't want the Internet Explorer or the classic control panel
                    if (explorerWindow.Document is not Shell32.IShellFolderViewDual2)
                    {
                        return null;
                    }
                    if (Path.GetFileName(explorerWindow.FullName) != "explorer.exe")
                    {
                        return null;
                    }

                    return explorerWindow;
                }
            }

            return null;
        }

        // COM Imports

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    }
}
