using Flow.Launcher.Plugin.Everything.Everything;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    public static class EverythingApiDllImport
    {
        public static void Load(string directory)
        {
            var path = Path.Combine(directory, DLL);
            int code = LoadLibrary(path);
            if (code == 0)
            {
                int err = Marshal.GetLastPInvokeError();
                Marshal.ThrowExceptionForHR(err);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int LoadLibrary(string name);

        private const string DLL = "Everything.dll";

        [DllImport(DLL, CharSet = CharSet.Unicode)]
        internal static extern int Everything_SetSearchW(string lpSearchString);

        [DllImport(DLL)]
        internal static extern void Everything_SetMatchPath(bool bEnable);

        [DllImport(DLL)]
        internal static extern void Everything_SetMatchCase(bool bEnable);

        [DllImport(DLL)]
        internal static extern void Everything_SetMatchWholeWord(bool bEnable);

        [DllImport(DLL)]
        internal static extern void Everything_SetRegex(bool bEnable);

        [DllImport(DLL)]
        internal static extern void Everything_SetMax(int dwMax);

        [DllImport(DLL)]
        internal static extern void Everything_SetOffset(int dwOffset);

        [DllImport(DLL)]
        internal static extern bool Everything_GetMatchPath();

        [DllImport(DLL)]
        internal static extern bool Everything_GetMatchCase();

        [DllImport(DLL)]
        internal static extern bool Everything_GetMatchWholeWord();

        [DllImport(DLL)]
        internal static extern bool Everything_GetRegex();

        [DllImport(DLL)]
        internal static extern uint Everything_GetMax();

        [DllImport(DLL)]
        internal static extern uint Everything_GetOffset();

        [DllImport(DLL, CharSet = CharSet.Unicode)]
        internal static extern string Everything_GetSearchW();

        [DllImport(DLL)]
        internal static extern EverythingApi.StateCode Everything_GetLastError();

        [DllImport(DLL, CharSet = CharSet.Unicode)]
        internal static extern bool Everything_QueryW(bool bWait);

        [DllImport(DLL)]
        internal static extern void Everything_SortResultsByPath();

        [DllImport(DLL)]
        internal static extern int Everything_GetNumFileResults();
        
        [DllImport(DLL)]
        internal static extern int Everything_GetMajorVersion();
        
        [DllImport(DLL)]
        internal static extern int Everything_GetNumFolderResults();

        [DllImport(DLL)]
        internal static extern int Everything_GetNumResults();

        [DllImport(DLL)]
        internal static extern int Everything_GetTotFileResults();

        [DllImport(DLL)]
        internal static extern int Everything_GetTotFolderResults();

        [DllImport(DLL)]
        internal static extern int Everything_GetTotResults();

        [DllImport(DLL)]
        internal static extern bool Everything_IsVolumeResult(int nIndex);

        [DllImport(DLL)]
        internal static extern bool Everything_IsFolderResult(int nIndex);

        [DllImport(DLL)]
        internal static extern bool Everything_IsFileResult(int nIndex);

        [DllImport(DLL, CharSet = CharSet.Unicode)]
        internal static extern void Everything_GetResultFullPathNameW(int nIndex, StringBuilder lpString, int nMaxCount);

        [DllImport(DLL)]
        internal static extern void Everything_Reset();

        // Everything 1.4

        [DllImport(DLL)]
        public static extern void Everything_SetSort(SortOption dwSortType);
        [DllImport(DLL)]
        public static extern bool Everything_IsFastSort(SortOption dwSortType);
        [DllImport(DLL)]
        public static extern SortOption Everything_GetSort();
        [DllImport(DLL)]
        public static extern uint Everything_GetResultListSort();
        [DllImport(DLL)]
        public static extern void Everything_SetRequestFlags(uint dwRequestFlags);
        [DllImport(DLL)]
        public static extern uint Everything_GetRequestFlags();
        [DllImport(DLL)]
        public static extern uint Everything_GetResultListRequestFlags();
        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultExtension(uint nIndex);
        [DllImport(DLL)]
        public static extern bool Everything_GetResultSize(uint nIndex, out long lpFileSize);
        [DllImport(DLL)]
        public static extern bool Everything_GetResultDateCreated(uint nIndex, out long lpFileTime);
        [DllImport(DLL)]
        public static extern bool Everything_GetResultDateModified(uint nIndex, out long lpFileTime);
        [DllImport(DLL)]
        public static extern bool Everything_GetResultDateAccessed(uint nIndex, out long lpFileTime);
        [DllImport(DLL)]
        public static extern uint Everything_GetResultAttributes(uint nIndex);
        [DllImport(DLL, CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultFileListFileName(uint nIndex);
        [DllImport(DLL)]
        public static extern uint Everything_GetResultRunCount(uint nIndex);
        [DllImport(DLL)]
        public static extern bool Everything_GetResultDateRun(uint nIndex, out long lpFileTime);
        [DllImport(DLL)]
        public static extern bool Everything_GetResultDateRecentlyChanged(uint nIndex, out long lpFileTime);
        [DllImport(DLL, CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultHighlightedFileName(uint nIndex);
        [DllImport(DLL, CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultHighlightedPath(uint nIndex);
        [DllImport(DLL, CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultHighlightedFullPathAndFileName(uint nIndex);
        [DllImport(DLL)]
        public static extern uint Everything_GetRunCountFromFileName(string lpFileName);
        [DllImport(DLL)]
        public static extern bool Everything_SetRunCountFromFileName(string lpFileName, uint dwRunCount);
        [DllImport(DLL)]
        public static extern uint Everything_IncRunCountFromFileName(string lpFileName);
    }
}
