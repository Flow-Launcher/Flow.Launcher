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
            string locationUrl = explorerWindow?.LocationURL;
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
        private static dynamic GetActiveExplorer()
        {
            // get the active window
            IntPtr handle = GetForegroundWindow();

            Type type = Type.GetTypeFromProgID("Shell.Application");
            if (type == null) return null;
            dynamic shell = Activator.CreateInstance(type);
            var openWindows = shell.Windows();
            for (int i = 0; i < openWindows.Count; i++)
            {
                var window = openWindows.Item(i);
                if (window == null) continue;

                // find the desired window and make sure that it is indeed a file explorer
                // we don't want the Internet Explorer or the classic control panel
                if (Path.GetFileName((string)window.FullName) == "explorer.exe" && new IntPtr(window.HWND) == handle)
                {
                    return window;
                }
            }

            return null;
        }

        // COM Imports

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
    }
}
