using Flow.Launcher.Plugin.Everything.Everything;
using Flow.Launcher.Plugin.Explorer.Search.Everything.Exceptions;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{

    public static class EverythingApi
    {

        private const int BufferSize = 4096;

        private static readonly object syncObject = new object();
        // cached buffer to remove redundant allocations.
        private static readonly StringBuilder buffer = new StringBuilder(BufferSize);

        public enum StateCode
        {
            OK,
            MemoryError,
            IPCError,
            RegisterClassExError,
            CreateWindowError,
            CreateThreadError,
            InvalidIndexError,
            InvalidCallError
        }

        /// <summary>
        /// Gets or sets a value indicating whether [match path].
        /// </summary>
        /// <value><c>true</c> if [match path]; otherwise, <c>false</c>.</value>
        public static bool MatchPath
        {
            get => EverythingApiDllImport.Everything_GetMatchPath();
            set => EverythingApiDllImport.Everything_SetMatchPath(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether [match case].
        /// </summary>
        /// <value><c>true</c> if [match case]; otherwise, <c>false</c>.</value>
        public static bool MatchCase
        {
            get => EverythingApiDllImport.Everything_GetMatchCase();
            set => EverythingApiDllImport.Everything_SetMatchCase(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether [match whole word].
        /// </summary>
        /// <value><c>true</c> if [match whole word]; otherwise, <c>false</c>.</value>
        public static bool MatchWholeWord
        {
            get => EverythingApiDllImport.Everything_GetMatchWholeWord();
            set => EverythingApiDllImport.Everything_SetMatchWholeWord(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether [enable regex].
        /// </summary>
        /// <value><c>true</c> if [enable regex]; otherwise, <c>false</c>.</value>
        public static bool EnableRegex
        {
            get => EverythingApiDllImport.Everything_GetRegex();
            set => EverythingApiDllImport.Everything_SetRegex(value);
        }

        /// <summary>
        /// Resets this instance.
        /// </summary>
        private static void Reset()
        {
            lock (syncObject)
            {
                EverythingApiDllImport.Everything_Reset();
            }
        }

        /// <summary>
        /// Checks whether the sort option is Fast Sort.
        /// </summary>
        public static bool IsFastSortOption(SortOption sortOption)
        {
            var fastSortOptionEnabled = EverythingApiDllImport.Everything_IsFastSort(sortOption);

            // If the Everything service is not running, then this call will incorrectly report 
            // the state as false. This checks for errors thrown by the api and up to the caller to handle.
            CheckAndThrowExceptionOnError();

            return fastSortOptionEnabled;
        }

        /// <summary>
        /// Searches the specified key word and reset the everything API afterwards
        /// </summary>
        /// <param name="keyword">The key word.</param>
        /// <param name="token">when cancelled the current search will stop and exit (and would not reset)</param>
        /// <param name="sortOption">Sort By</param>
        /// <param name="offset">The offset.</param>
        /// <param name="maxCount">The max count.</param>
        /// <returns></returns>
        public static IEnumerable<SearchResult> SearchAsync(string keyword, CancellationToken token, SortOption sortOption = SortOption.NAME_ASCENDING, int offset = 0, int maxCount = 100)
        {
            if (string.IsNullOrEmpty(keyword))
                throw new ArgumentNullException(nameof(keyword));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (maxCount < 0)
                throw new ArgumentOutOfRangeException(nameof(maxCount));

            lock (syncObject)
            {
                if (keyword.StartsWith("@"))
                {
                    EverythingApiDllImport.Everything_SetRegex(true);
                    keyword = keyword[1..];
                }

                EverythingApiDllImport.Everything_SetSearchW(keyword);
                EverythingApiDllImport.Everything_SetOffset(offset);
                EverythingApiDllImport.Everything_SetMax(maxCount);

                EverythingApiDllImport.Everything_SetSort(sortOption);

                if (token.IsCancellationRequested)
                {
                    return null;
                }


                if (!EverythingApiDllImport.Everything_QueryW(true))
                {
                    CheckAndThrowExceptionOnError();
                    return null;
                }

                var results = new List<SearchResult>();
                for (var idx = 0; idx < EverythingApiDllImport.Everything_GetNumResults(); ++idx)
                {
                    if (token.IsCancellationRequested)
                    {
                        return null;
                    }

                    EverythingApiDllImport.Everything_GetResultFullPathNameW(idx, buffer, BufferSize);

                    var result = new SearchResult
                    {
                        FullPath = buffer.ToString(),
                        Type = EverythingApiDllImport.Everything_IsFolderResult(idx) ? ResultType.Folder :
                            EverythingApiDllImport.Everything_IsFileResult(idx) ? ResultType.File :
                            ResultType.Volume
                    };

                    results.Add(result);
                }

                Reset();

                return results;
            }
        }

        private static void CheckAndThrowExceptionOnError()
        {
            switch (EverythingApiDllImport.Everything_GetLastError())
            {
                case StateCode.CreateThreadError:
                    throw new CreateThreadException();
                case StateCode.CreateWindowError:
                    throw new CreateWindowException();
                case StateCode.InvalidCallError:
                    throw new InvalidCallException();
                case StateCode.InvalidIndexError:
                    throw new InvalidIndexException();
                case StateCode.IPCError:
                    throw new IPCErrorException();
                case StateCode.MemoryError:
                    throw new MemoryErrorException();
                case StateCode.RegisterClassExError:
                    throw new RegisterClassExException();
            }
        }
    }
}