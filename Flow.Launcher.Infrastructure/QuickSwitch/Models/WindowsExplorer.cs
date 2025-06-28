using System;
using System.Runtime.InteropServices;
using Flow.Launcher.Plugins;
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
        private static readonly string ClassName = nameof(WindowsExplorer);

        private static IWebBrowser2 _lastExplorerView = null;
        private static readonly object _lastExplorerViewLock = new();

        public bool CheckExplorerWindow(IntPtr foreground)
        {
            var isExplorer = false;
            // Is it from Explorer?
            var processName = Win32Helper.GetProcessNameFromHwnd(new(foreground));
            if (processName.ToLower() == "explorer.exe")
            {
                EnumerateShellWindows((shellWindow) =>
                {
                    try
                    {
                        if (shellWindow is not IWebBrowser2 explorer) return true;

                        if (explorer.HWND != foreground) return true;

                        lock (_lastExplorerViewLock)
                        {
                            _lastExplorerView = explorer;
                        }
                        isExplorer = true;
                        return false;
                    }
                    catch (COMException)
                    {
                        // Ignored
                    }

                    return true;
                });
            }
            return isExplorer;
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

        public string GetExplorerPath()
        {
            if (_lastExplorerView == null) return null;

            object document = null;
            try
            {
                lock (_lastExplorerViewLock)
                {
                    if (_lastExplorerView != null)
                    {
                        // Use dynamic here because using IWebBrower2.Document can cause exception here:
                        // System.Runtime.InteropServices.InvalidOleVariantTypeException: 'Specified OLE variant is invalid.'
                        dynamic explorerView = _lastExplorerView;
                        document = explorerView.Document;
                    }
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

        public void RemoveExplorerWindow()
        {
            lock (_lastExplorerViewLock)
            {
                _lastExplorerView = null;
            }
        }

        public void Dispose()
        {
            // Release ComObjects
            try
            {
                lock (_lastExplorerViewLock)
                {
                    if (_lastExplorerView != null)
                    {
                        Marshal.ReleaseComObject(_lastExplorerView);
                        _lastExplorerView = null;
                    }
                }
            }
            catch (COMException)
            {
                _lastExplorerView = null;
            }
        }
    }
}
