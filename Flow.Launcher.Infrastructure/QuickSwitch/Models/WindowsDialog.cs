using Flow.Launcher.Infrastructure.QuickSwitch.Interface;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Flow.Launcher.Infrastructure.QuickSwitch.Models
{
    internal class WindowsDialog : IQuickSwitchDialog
    {
        // The class name of a dialog window
        private const string WindowsDialogClassName = "#32770";

        public IQuickSwitchDialogWindow DialogWindow { get; private set; }

        public bool CheckDialogWindow(HWND hwnd)
        {
            if (GetWindowClassName(hwnd) == WindowsDialogClassName)
            {
                if (DialogWindow == null || DialogWindow.Handle != hwnd)
                {
                    DialogWindow = new WindowsDialogWindow(hwnd);
                }
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            DialogWindow?.Dispose();
            DialogWindow = null;
        }

        public static string GetWindowClassName(HWND handle)
        {
            return GetClassName(handle);

            static unsafe string GetClassName(HWND handle)
            {
                fixed (char* buf = new char[256])
                {
                    return PInvoke.GetClassName(handle, buf, 256) switch
                    {
                        0 => null,
                        _ => new string(buf),
                    };
                }
            }
        }
    }

    internal class WindowsDialogWindow : IQuickSwitchDialogWindow
    {
        public HWND Handle { get; private set; }

        public WindowsDialogWindow(HWND handle)
        {
            Handle = handle;
        }

        public IQuickSwitchDialogTab GetCurrentTab()
        {
            return new WindowsDialogTab(Handle);
        }

        public void Dispose()
        {
            Handle = HWND.Null;
        }
    }

    internal class WindowsDialogTab : IQuickSwitchDialogTab
    {
        public HWND Handle { get; private set; }

        public WindowsDialogTab(HWND handle)
        {
            Handle = handle;
        }

        public string GetCurrentFolder()
        {
            // TODO
            return string.Empty;
        }

        public string GetCurrentFile()
        {
            // TODO
            return string.Empty;
        }

        public bool OpenFolder(string path)
        {
            return false;
        }

        public void Dispose()
        {
            Handle = HWND.Null;
        }
    }
}
