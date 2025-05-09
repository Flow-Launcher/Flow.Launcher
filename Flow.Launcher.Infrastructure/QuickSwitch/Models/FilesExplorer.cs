using System;
using System.IO;
using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.QuickSwitch.Interface;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace Flow.Launcher.Infrastructure.QuickSwitch.Models
{
    /// <summary>
    /// Class for handling Files instances in QuickSwitch.
    /// </summary>
    /// <remarks>
    /// Edited from: https://github.com/files-community/Listary.FileAppPlugin.Files
    /// </remarks>
    internal class FilesExplorer : IQuickSwitchExplorer
    {
        private static readonly string ClassName = nameof(FilesExplorer);

        private static FilesWindow _lastExplorerView = null;
        private static readonly object _lastExplorerViewLock = new();

        public bool CheckExplorerWindow(HWND hWnd)
        {
            var isExplorer = false;
            lock (_lastExplorerViewLock)
            {
                // Is it from Files?
                var processName = Path.GetFileName(GetProcessPathFromHwnd(hWnd));
                if (processName == "Files.exe")
                {
                    // Is it Files's file window?
                    try
                    {
                        var automation = new UIA3Automation();
                        var Files = automation.FromHandle(hWnd);
                        if (Files.Name == "Files" || Files.Name.Contains("- Files"))
                        {
                            _lastExplorerView = new FilesWindow(hWnd, automation, Files);
                            isExplorer = true;
                        }
                    }
                    catch (TimeoutException e)
                    {
                        Log.Warn(ClassName, $"UIA timeout: {e}");
                    }
                    catch (System.Exception e)
                    {
                        Log.Warn(ClassName, $"Failed to bind window: {e}");
                    }
                }
            }
            return isExplorer;
        }

        private static unsafe string GetProcessPathFromHwnd(HWND hWnd)
        {
            uint pid;
            var threadId = PInvoke.GetWindowThreadProcessId(hWnd, &pid);
            if (threadId == 0) return string.Empty;

            var process = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (process.Value != IntPtr.Zero)
            {
                using var safeHandle = new SafeProcessHandle(process.Value, true);
                uint capacity = 2000;
                Span<char> buffer = new char[capacity];
                fixed (char* pBuffer = buffer)
                {
                    if (!PInvoke.QueryFullProcessImageName(safeHandle, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, (PWSTR)pBuffer, ref capacity))
                    {
                        return string.Empty;
                    }

                    return buffer[..(int)capacity].ToString();
                }
            }

            return string.Empty;
        }

        public string GetExplorerPath()
        {
            if (_lastExplorerView == null) return null;
            return _lastExplorerView.GetCurrentTab().GetCurrentFolder();
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
                        _lastExplorerView.Dispose();
                        _lastExplorerView = null;
                    }
                }
            }
            catch (COMException)
            {
                _lastExplorerView = null;
            }
        }

        private class FilesWindow : IDisposable
        {
            private readonly UIA3Automation _automation;
            private readonly AutomationElement _Files;

            public IntPtr Handle { get; }

            public FilesWindow(IntPtr hWnd, UIA3Automation automation, AutomationElement Files)
            {
                Handle = hWnd;
                _automation = automation;
                _Files = Files;
            }

            public void Dispose()
            {
                _automation.Dispose();
            }

            public FilesTab GetCurrentTab()
            {
                return new FilesTab(_Files);
            }
        }

        private class FilesTab
        {
            private readonly TextBox _currentPathGet;
            private readonly TextBox _currentPathSet;

            public FilesTab(AutomationElement Files)
            {
                // Find window content to reduce the scope
                var _windowContent = Files.FindFirstChild(cf => cf.ByClassName("Microsoft.UI.Content.DesktopChildSiteBridge"));

                _currentPathGet = _windowContent.FindFirstChild(cf => cf.ByAutomationId("CurrentPathGet"))?.AsTextBox();
                if (_currentPathGet == null)
                {
                    Log.Error(ClassName, "Failed to find CurrentPathGet");
                    return;
                }

                _currentPathSet = _windowContent.FindFirstChild(cf => cf.ByAutomationId("CurrentPathSet"))?.AsTextBox();
                if (_currentPathSet == null)
                {
                    Log.Error(ClassName, "Failed to find CurrentPathSet");
                    return;
                }
            }

            public string GetCurrentFolder()
            {
                try
                {
                    return _currentPathGet.Text;
                }
                catch (System.Exception e)
                {
                    Log.Error(ClassName, $"Failed to get current folder: {e}");
                    return null;
                }
            }

            public bool OpenFolder(string path)
            {
                try
                {
                    _currentPathSet.Text = path;
                    return true;
                }
                catch (System.Exception e)
                {
                    Log.Error(ClassName, $"Failed to get current folder: {e}");
                    return false;
                }
            }
        }
    }
}
