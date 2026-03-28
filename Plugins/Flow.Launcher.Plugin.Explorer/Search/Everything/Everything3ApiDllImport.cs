using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    internal static class Everything3ApiDllImport
    {
        private static IntPtr _dllHandle = IntPtr.Zero;
        internal static bool IsLoaded => _dllHandle != IntPtr.Zero;

        public static void Load(string directory)
        {
            if (_dllHandle != IntPtr.Zero)
            {
                return;
            }

            var path = Path.Combine(directory, Dll);
            _dllHandle = LoadLibrary(path);
            if (_dllHandle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
        }

        public static void Unload()
        {
            if (_dllHandle == IntPtr.Zero)
            {
                return;
            }

            _ = FreeLibrary(_dllHandle);
            _dllHandle = IntPtr.Zero;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);

        private const string Dll = "Everything3.dll";

        [DllImport(Dll, CharSet = CharSet.Unicode)]
        internal static extern IntPtr Everything3_ConnectW(string instanceName);

        [DllImport(Dll)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Everything3_DestroyClient(IntPtr client);

        [DllImport(Dll)]
        internal static extern IntPtr Everything3_CreateSearchState();

        [DllImport(Dll)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Everything3_DestroySearchState(IntPtr searchState);

        [DllImport(Dll, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Everything3_SetSearchTextW(IntPtr searchState, string search);

        [DllImport(Dll)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Everything3_SetSearchRegex(IntPtr searchState, bool matchRegex);

        [DllImport(Dll)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Everything3_SetSearchMatchPath(IntPtr searchState, bool matchPath);

        [DllImport(Dll)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Everything3_SetSearchHideResultOmissions(IntPtr searchState, bool hideResultOmissions);

        [DllImport(Dll)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Everything3_SetSearchViewportOffset(IntPtr searchState, nuint offset);

        [DllImport(Dll)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Everything3_SetSearchViewportCount(IntPtr searchState, nuint count);

        [DllImport(Dll)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Everything3_ClearSearchSorts(IntPtr searchState);

        [DllImport(Dll)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Everything3_AddSearchSort(IntPtr searchState, uint propertyId, bool ascending);

        [DllImport(Dll)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Everything3_ClearSearchPropertyRequests(IntPtr searchState);

        [DllImport(Dll)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Everything3_AddSearchPropertyRequest(IntPtr searchState, uint propertyId);

        [DllImport(Dll)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Everything3_AddSearchPropertyRequestHighlighted(IntPtr searchState, uint propertyId);

        [DllImport(Dll)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Everything3_GetSearchPropertyRequestHighlight(IntPtr searchState, nuint index);

        [DllImport(Dll)]
        internal static extern IntPtr Everything3_Search(IntPtr client, IntPtr searchState);

        [DllImport(Dll)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Everything3_DestroyResultList(IntPtr resultList);

        [DllImport(Dll)]
        internal static extern nuint Everything3_GetResultListViewportCount(IntPtr resultList);

        [DllImport(Dll)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Everything3_IsFolderResult(IntPtr resultList, nuint resultIndex);

        [DllImport(Dll)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Everything3_IsRootResult(IntPtr resultList, nuint resultIndex);

        [DllImport(Dll, CharSet = CharSet.Unicode)]
        internal static extern nuint Everything3_GetResultFullPathNameW(IntPtr resultList, nuint resultIndex, StringBuilder buffer, nuint bufferSizeInWChars);

        [DllImport(Dll, CharSet = CharSet.Unicode)]
        internal static extern nuint Everything3_GetResultPropertyTextHighlightedW(IntPtr resultList, nuint resultIndex, uint propertyId, StringBuilder buffer, nuint bufferSizeInWChars);

        [DllImport(Dll, CharSet = CharSet.Unicode)]
        internal static extern nuint Everything3_GetResultPropertyTextW(IntPtr resultList, nuint resultIndex, uint propertyId, StringBuilder buffer, nuint bufferSizeInWChars);

        [DllImport(Dll)]
        internal static extern uint Everything3_GetResultRunCount(IntPtr resultList, nuint resultIndex);

        [DllImport(Dll, CharSet = CharSet.Unicode)]
        internal static extern uint Everything3_IncRunCountFromFilenameW(IntPtr client, string fileName);

        [DllImport(Dll)]
        internal static extern uint Everything3_GetLastError();


        [DllImport(Dll)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Everything3_IsPropertyFastSort(IntPtr client, uint propertyId);
    }
}
