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
            Span<char> buffer = stackalloc char[MAX_PATH];

            var data = new WIN32_FIND_DATAW();
            var target = string.Empty;
            try
            {
                fixed (char* bufferPtr = buffer)
                {
                    ((IShellLinkW)link).GetPath((PWSTR)bufferPtr, MAX_PATH, &data, (uint)SLGP_FLAGS.SLGP_SHORTPATH);
                    target = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(bufferPtr).ToString();
                }
            }
            catch (COMException e)
            {
                ProgramLogger.LogException($"|IShellLinkW|retrieveTargetPath|{path}" +
                "|Error occurred while getting program arguments", e);
            }

            // To set the app description
            if (!string.IsNullOrEmpty(target))
            {
                try
                {
                    fixed (char* bufferPtr = buffer)
                    {
                        ((IShellLinkW)link).GetDescription(bufferPtr, MAX_PATH);
                        description = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(bufferPtr).ToString();
                    }
                }
                catch (COMException e)
                {
                    // C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Programs\\MiracastView.lnk always cause exception
                    ProgramLogger.LogException($"|IShellLinkW|retrieveTargetPath|{path}" +
                                               "|Error caused likely due to trying to get the description of the program",
                        e);
                }

                fixed (char* bufferPtr = buffer)
                {
                    ((IShellLinkW)link).GetArguments(bufferPtr, MAX_PATH);
                    arguments = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(bufferPtr).ToString();
                }
            }

            // To release unmanaged memory
            Marshal.ReleaseComObject(link);

            return target;
            }
    }
}
