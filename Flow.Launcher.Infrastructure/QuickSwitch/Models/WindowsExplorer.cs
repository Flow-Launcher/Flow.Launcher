using System;
using System.Runtime.InteropServices;
using System.Threading;
using Flow.Launcher.Plugin;
using Windows.Win32;
using Windows.Win32.Foundation;
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

                        explorerWindow = new WindowsExplorerWindow(hwnd);
                        return false;
                    }
                    catch
                    {
                        // Ignored
                    }

                    return true;
                });
            }
            return explorerWindow;
        }

        internal static unsafe void EnumerateShellWindows(Func<object, bool> action)
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

        private static Guid _shellBrowserGuid = typeof(IShellBrowser).GUID;

        internal WindowsExplorerWindow(IntPtr handle)
        {
            Handle = handle;
        }

        public string GetExplorerPath()
        {
            if (Handle == IntPtr.Zero) return null;

            var activeTabHandle = GetActiveTabHandle(new(Handle));
            if (activeTabHandle.IsNull) return null;

            var window = GetExplorerByTabHandle(activeTabHandle);
            if (window == null) return null;

            var path = GetLocation(window);
            return path;
        }

        public void Dispose()
        {

        }

        #region Helper Methods

        // Inspired by: https://github.com/w4po/ExplorerTabUtility

        private static HWND GetActiveTabHandle(HWND windowHandle)
        {
            // Active tab always at the top of the z-index, so it is the first child of the ShellTabWindowClass.
            var activeTab = PInvoke.FindWindowEx(windowHandle, HWND.Null, "ShellTabWindowClass", null);
            return activeTab;
        }

        private static IWebBrowser2 GetExplorerByTabHandle(HWND tabHandle)
        {
            if (tabHandle.IsNull) return null;

            IWebBrowser2 window = null;
            WindowsExplorer.EnumerateShellWindows((shellWindow) =>
            {
                try
                {
                    return StartSTAThread(() =>
                    {
                        if (shellWindow is not IWebBrowser2 explorer) return true;

                        if (explorer is not IServiceProvider sp) return true;

                        sp.QueryService(ref _shellBrowserGuid, ref _shellBrowserGuid, out var shellBrowser);
                        if (shellBrowser == null) return true;

                        try
                        {
                            shellBrowser.GetWindow(out var hWnd); // Must execute in STA thread to get this hWnd

                            if (hWnd == tabHandle)
                            {
                                window = explorer;
                                return false;
                            }
                        }
                        catch
                        {
                            // Ignored   
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(shellBrowser);
                        }

                        return true;
                    }) ?? true;
                }
                catch
                {
                    // Ignored
                }

                return true;
            });

            return window;
        }

        private static bool? StartSTAThread(Func<bool> action)
        {
            bool? result = null;
            var thread = new Thread(() =>
            {
                result = action();
            })
            {
                IsBackground = true
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return result;
        }

        private static string GetLocation(IWebBrowser2 window)
        {
            var path = window.LocationURL.ToString();
            if (!string.IsNullOrWhiteSpace(path)) return NormalizeLocation(path);

            // Recycle Bin, This PC, etc
            if (window.Document is not IShellFolderViewDual folderView) return null;

            // Attempt to get the path from the folder view
            try
            {
                // CSWin32 Folder does not have Self, so we need to use dynamic type here
                // Use dynamic to bypass static typing
                dynamic folder = folderView.Folder;

                // Access the Self property via dynamic binding
                dynamic folderItem = folder.Self;

                // Get path from the folder item
                path = folderItem.Path;
            }
            catch
            {
                return null;
            }

            return NormalizeLocation(path);
        }

        private static string NormalizeLocation(string location)
        {
            if (location.IndexOf('%') > -1)
                location = Environment.ExpandEnvironmentVariables(location);

            if (location.StartsWith("::", StringComparison.Ordinal))
                location = $"shell:{location}";

            else if (location.StartsWith("{", StringComparison.Ordinal))
                location = $"shell:::{location}";

            location = location.Trim(' ', '/', '\\', '\n', '\'', '"');

            return location.Replace('/', '\\');
        }

        #endregion
    }

    #region COM Interfaces

    // Inspired by: https://github.com/w4po/ExplorerTabUtility

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("6d5140c1-7436-11ce-8034-00aa006009fa")]
    [ComImport]
    public interface IServiceProvider
    {
        [PreserveSig]
        int QueryService(ref Guid guidService, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellBrowser ppvObject);
    }

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E2-0000-0000-C000-000000000046")]
    [ComImport]
    public interface IShellBrowser
    {
        [PreserveSig]
        int GetWindow(out nint handle);
    }

    #endregion
}
