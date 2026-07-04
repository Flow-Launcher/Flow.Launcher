using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Flow.Launcher.Plugin.Program.Logger;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Com.StructuredStorage;
using Windows.Win32.System.Variant;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.PropertiesSystem;
using Windows.Win32.Storage.FileSystem;

namespace Flow.Launcher.Plugin.Program.Programs
{
    public static class ShellLinkReader
    {
        // Retrieves the target path, arguments, and description from a shell link
        public static ShellLinkReadResult Read(string path)
        {
            var link = new ShellLink();
            try
            {
                try
                {
                    ((IPersistFile)link).Load(path, (int)STGM.STGM_READ);
                    var hwnd = new HWND(IntPtr.Zero);
                    // Use SLR_NO_UI to avoid showing any UI during resolution, like Problem with Shortcut dialogs
                    // https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishelllinka-resolve
                    ((IShellLinkW)link).Resolve(hwnd, (uint)SLR_FLAGS.SLR_NO_UI);
                }
                catch (COMException e)
                {
                    ProgramLogger.LogException(
                        $"|ShellLinkReader|Read|{path}|Error occurred while loading or resolving shell link",
                        e
                    );
                    return new ShellLinkReadResult(string.Empty, string.Empty, string.Empty);
                }

                var target = retrieveTargetPath((IShellLinkW)link, path);
                var description = retrieveDescription((IShellLinkW)link, path);
                var arguments = retrieveArguments((IPropertyStore)link, path);
                return new ShellLinkReadResult(target, description, arguments);
            }
            finally
            {
                // release unmanaged memory
                Marshal.ReleaseComObject(link);
            }
        }

        private static unsafe string retrieveTargetPath(IShellLinkW shellLink, string path)
        {
            var data = new WIN32_FIND_DATAW();
            try
            {
                Span<char> targetBuffer = stackalloc char[(int)PInvoke.MAX_PATH];
                targetBuffer.Clear();
                fixed (char* targetBufferPtr = targetBuffer)
                {
                    shellLink.GetPath((PWSTR)targetBufferPtr, (int)PInvoke.MAX_PATH, &data, (uint)SLGP_FLAGS.SLGP_SHORTPATH);
                    return MemoryMarshal.CreateReadOnlySpanFromNullTerminated(targetBufferPtr).ToString();
                }
            }
            catch (COMException e)
            {
                ProgramLogger.LogException($"|ShellLinkReader|retrieveTargetPath|{path}" +
                    "|Error occurred while getting program target path from shell link", e);
                return string.Empty;
            }
        }

        private static unsafe string retrieveDescription(IShellLinkW shellLink, string path)
        {
            try
            {
                Span<char> descriptionBuffer = stackalloc char[(int)PInvoke.INFOTIPSIZE];
                descriptionBuffer.Clear();
                fixed (char* descriptionBufferPtr = descriptionBuffer)
                {
                    shellLink.GetDescription(descriptionBufferPtr, (int)PInvoke.INFOTIPSIZE);
                    return MemoryMarshal.CreateReadOnlySpanFromNullTerminated(descriptionBufferPtr).ToString();
                }
            }
            catch (COMException e)
            {
                // C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Programs\\MiracastView.lnk always cause exception
                ProgramLogger.LogException(
                    $"|ShellLinkReader|retrieveDescription|{path}" +
                    "|Error caused likely due to trying to get the description of the program",
                    e
                );
                return string.Empty;
            }
        }

        private static string retrieveArguments(IPropertyStore shellLinkPropertyStore, string path)
        {
            PROPVARIANT argumentsProperty = new PROPVARIANT();

            try
            {
                var argumentsKey = PInvoke.PKEY_Link_Arguments;
                shellLinkPropertyStore.GetValue(in argumentsKey, out argumentsProperty);

                // CsWin32 preserves native C unions, so nested union fields are generated as "Anonymous".
                // see structure at https://learn.microsoft.com/en-ie/windows/win32/api/propidlbase/ns-propidlbase-propvariant#syntax
                var propVariantHeader = argumentsProperty.Anonymous.Anonymous;
                var propVariantValueUnion = propVariantHeader.Anonymous;
                var propVariantType = propVariantHeader.vt;
                
                return propVariantType switch
                {
                    VARENUM.VT_EMPTY => string.Empty,
                    VARENUM.VT_LPWSTR => propVariantValueUnion.pwszVal.ToString(),
                    _ => string.Empty
                };
            }
            catch (COMException e)
            {
                ProgramLogger.LogException($"|ShellLinkReader|retrieveArguments|{path}|Error occurred while getting program arguments", e);
                return string.Empty;
            }
            finally
            {
                PInvoke.PropVariantClear(ref argumentsProperty);
            }
        }
    }
}
