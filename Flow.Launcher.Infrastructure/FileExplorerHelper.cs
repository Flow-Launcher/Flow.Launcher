using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Win32;

namespace Flow.Launcher.Infrastructure
{
    public static class FileExplorerHelper
    {
        /// <summary>
        /// Gets the path of the file explorer that is currently in the foreground
        /// </summary>
        public static string GetActiveExplorerPath()
        {
            var explorerWindow = GetActiveExplorer();
            string locationUrl = explorerWindow?.LocationURL;
            return !string.IsNullOrEmpty(locationUrl) ? new Uri(locationUrl).LocalPath + "\\" : null;
        }

        /// <summary>
        /// Gets the file explorer that is currently in the foreground
        /// </summary>
        private static dynamic GetActiveExplorer()
        {
            Type type = Type.GetTypeFromProgID("Shell.Application");
            if (type == null) return null;
            dynamic shell = Activator.CreateInstance(type);
            if (shell == null)
            {
                return null;
            }

            var explorerWindows = new List<dynamic>();
            var openWindows = shell.Windows();
            for (int i = 0; i < openWindows.Count; i++)
            {
                var window = openWindows.Item(i);
                if (window == null) continue;

                // find the desired window and make sure that it is indeed a file explorer
                // we don't want the Internet Explorer or the classic control panel
                // ToLower() is needed, because Windows can report the path as "C:\\Windows\\Explorer.EXE"
                if (Path.GetFileName((string)window.FullName)?.ToLower() == "explorer.exe")
                {
                    explorerWindows.Add(window);
                }
            }

            if (explorerWindows.Count == 0) return null;

            var zOrders = GetZOrder(explorerWindows);

            return explorerWindows.Zip(zOrders).MinBy(x => x.Second).First;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        /// <summary>
        /// Gets the z-order for one or more windows atomically with respect to each other. In Windows, smaller z-order is higher. If the window is not top level, the z order is returned as -1. 
        /// </summary>
        private static IEnumerable<int> GetZOrder(List<dynamic> hWnds)
        {
            var z = new int[hWnds.Count];
            for (var i = 0; i < hWnds.Count; i++) z[i] = -1;

            var index = 0;
            var numRemaining = hWnds.Count;
            PInvoke.EnumWindows((wnd, _) =>
            {
                var searchIndex = hWnds.FindIndex(x => x.HWND == wnd.Value);
                if (searchIndex != -1)
                {
                    z[searchIndex] = index;
                    numRemaining--;
                    if (numRemaining == 0) return false;
                }
                index++;
                return true;
            }, IntPtr.Zero);

            return z;
        }
    }
}
