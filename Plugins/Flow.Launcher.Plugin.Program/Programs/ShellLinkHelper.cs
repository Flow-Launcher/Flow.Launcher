using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Flow.Launcher.Plugin.Program.Logger;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.Storage.FileSystem;

namespace Flow.Launcher.Plugin.Program.Programs
{
    class ShellLinkHelper
    {
        
        // Reference : http://www.pinvoke.net/default.aspx/Interfaces.IShellLinkW
        [ComImport(), Guid("00021401-0000-0000-C000-000000000046")]
        public class ShellLink
        {
        }

        // To initialize the app description
        public string description = string.Empty;
        public string arguments = string.Empty;

        // Retrieve the target path using Shell Link
        public unsafe string retrieveTargetPath(string path)
        {
            var link = new ShellLink();
            const int STGM_READ = 0;
            ((IPersistFile)link).Load(path, STGM_READ);
            var hwnd = new HWND(IntPtr.Zero);
            ((IShellLinkW)link).Resolve(hwnd, 0);

            const int MAX_PATH = 260;
            char[] buffer = new char[MAX_PATH];

            var data = new WIN32_FIND_DATAW();
            var target = string.Empty;
            fixed (char* bufferChar = buffer)
            {
                ((IShellLinkW)link).GetPath((PWSTR)bufferChar, MAX_PATH, &data, (uint)SLGP_FLAGS.SLGP_SHORTPATH);

                // Truncate the buffer to the actual length of the string
                int validLength = Array.IndexOf(buffer, '\0');
                if (validLength < 0) validLength = MAX_PATH;
                target = new string(buffer, 0, validLength);
            }

            // To set the app description
            if (!string.IsNullOrEmpty(target))
            {
                try
                {
                    char[] buffer1 = new char[MAX_PATH];
                    fixed (char* buffer1Char = buffer1)
                    {
                        ((IShellLinkW)link).GetDescription((PWSTR)buffer1Char, MAX_PATH);
                        int validLength = Array.IndexOf(buffer1, '\0');
                        if (validLength < 0) validLength = MAX_PATH;
                        description = new string(buffer1, 0, validLength);
                    }
                }
                catch (COMException e)
                {
                    // C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Programs\\MiracastView.lnk always cause exception
                    ProgramLogger.LogException($"|IShellLinkW|retrieveTargetPath|{path}" +
                                               "|Error caused likely due to trying to get the description of the program",
                        e);
                }

                char[] buffer2 = new char[MAX_PATH];
                fixed (char* buffer2Char = buffer2)
                {
                    ((IShellLinkW)link).GetArguments((PWSTR)buffer2Char, MAX_PATH);
                    int validLength = Array.IndexOf(buffer2, '\0');
                    if (validLength < 0) validLength = MAX_PATH;
                    arguments = new string(buffer2, 0, validLength);
                }
            }
            
            // To release unmanaged memory
            Marshal.ReleaseComObject(link);

            return target;
        }
    }
}
