using System;
using System.Runtime.InteropServices;
using Flow.Launcher.Plugin;
using Windows.Win32;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace Flow.Launcher.Infrastructure.QuickSwitch.Models
{
    /// <summary>
    /// Class for handling Windows Explorer instances in QuickSwitch.
    /// </summary>
    public class WindowsExplorer : IQuickSwitchExplorer
    {
        public IQuickSwitchExplorerWindow CheckExplorerWindow(IntPtr hwnd)
        {
            IQuickSwitchExplorerWindow explorerWindow = null;

            // Is it from Explorer?
            var processName = Win32Helper.GetProcessNameFromHwnd(new(hwnd));
            if (processName.ToLower() == "explorer.exe")
            {
                EnumerateShellWindows((shellWindow) =>
                {
                    try
                    {
                        if (shellWindow is not IWebBrowser2 explorer) return true;

                        if (explorer.HWND != hwnd) return true;

                        explorerWindow = new WindowsExplorerWindow(hwnd, explorer);
                        return false;
                    }
                    catch (COMException)
                    {
                        // Ignored
                    }

                    return true;
                });
            }
            return explorerWindow;
        }

        private static unsafe void EnumerateShellWindows(Func<object, bool> action)
        {
            // Create an instance of ShellWindows
            var clsidShellWindows = new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"); // ShellWindowsClass
            var iidIShellWindows = typeof(IShellWindows).GUID; // IShellWindows

            var result = PInvoke.CoCreateInstance(
                &clsidShellWindows,
                null,
                CLSCTX.CLSCTX_ALL,
                &iidIShellWindows,
                out var shellWindowsObj);

            if (result.Failed) return;

            var shellWindows = (IShellWindows)shellWindowsObj;

            // Enumerate the shell windows
            var count = shellWindows.Count;
            for (var i = 0; i < count; i++)
            {
                if (!action(shellWindows.Item(i)))
                {
                    return;
                }
            }
        }

        public void Dispose()
        {
            
        }
    }

    public class WindowsExplorerWindow : IQuickSwitchExplorerWindow
    {
        public IntPtr Handle { get; }

        private static IWebBrowser2 _explorerView = null;

        internal WindowsExplorerWindow(IntPtr handle, IWebBrowser2 explorerView)
        {
            Handle = handle;
            _explorerView = explorerView;
        }

        public string GetExplorerPath()
        {
            if (_explorerView == null) return null;

            object document = null;
            try
            {
                if (_explorerView != null)
                {
                    // Use dynamic here because using IWebBrower2.Document can cause exception here:
                    // System.Runtime.InteropServices.InvalidOleVariantTypeException: 'Specified OLE variant is invalid.'
                    dynamic explorerView = _explorerView;
                    document = explorerView.Document;
                }
            }
            catch (COMException)
            {
                return null;
            }

            if (document is not IShellFolderViewDual2 folderView)
            {
                return null;
            }

            string path;
            try
            {
                // CSWin32 Folder does not have Self, so we need to use dynamic type here
                // Use dynamic to bypass static typing
                dynamic folder = folderView.Folder;

                // Access the Self property via dynamic binding
                dynamic folderItem = folder.Self;

                // Check if the item is part of the file system
                if (folderItem != null && folderItem.IsFileSystem)
                {
                    path = folderItem.Path;
                }
                else
                {
                    // Handle non-file system paths (e.g., virtual folders)
                    path = string.Empty;
                }
            }
            catch
            {
                return null;
            }

            return path;
        }

        public void Dispose()
        {
            // Release ComObjects
            try
            {
                if (_explorerView != null)
                {
                    Marshal.ReleaseComObject(_explorerView);
                    _explorerView = null;
                }
            }
            catch (COMException)
            {
                _explorerView = null;
            }
        }
    }
}
