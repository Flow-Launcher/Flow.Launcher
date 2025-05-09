using System;
using System.IO;
using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.QuickSwitch.Interface;
using Windows.Win32.Foundation;

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

        public bool CheckExplorerWindow(HWND foreground)
        {
            var isExplorer = false;
            // Is it from Files?
            var processName = Win32Helper.GetProcessPathFromHwnd(foreground);
            if (processName.ToLower() == "files.exe")
            {
                // Is it Files's file window?
                try
                {
                    var automation = new UIA3Automation();
                    var Files = automation.FromHandle(foreground);
                    var lowerFilesName = Files.Name.ToLower();
                    if (lowerFilesName == "files" || lowerFilesName.Contains("- files"))
                    {
                        lock (_lastExplorerViewLock)
                        {
                            _lastExplorerView = new FilesWindow(foreground, automation, Files);
                        }
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
            return isExplorer;
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
