using Flow.Launcher.Plugin.Everything.Everything;
using Flow.Launcher.Plugin.Explorer.Search.Everything.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms.Design;
using Flow.Launcher.Plugin.Explorer.Exceptions;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{

    public static class EverythingApi
    {

        private const int BufferSize = 4096;

        private static SemaphoreSlim _semaphore = new(1, 1);
        // cached buffer to remove redundant allocations.
        private static readonly StringBuilder buffer = new(BufferSize);

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

        public static async ValueTask<bool> IsEverythingRunningAsync(CancellationToken token = default)
        {
            await _semaphore.WaitAsync(token);

            try
            {
                EverythingApiDllImport.Everything_GetMajorVersion(); 
                var result = EverythingApiDllImport.Everything_GetLastError() != StateCode.IPCError;
                return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Searches the specified key word and reset the everything API afterwards
        /// </summary>
        /// <param name="option">Search Criteria</param>
        /// <param name="token">when cancelled the current search will stop and exit (and would not reset)</param>
        /// <returns>An IAsyncEnumerable that will enumerate all results searched by the specific query and option</returns>
        public static async IAsyncEnumerable<SearchResult> SearchAsync(EverythingSearchOption option,
            [EnumeratorCancellation] CancellationToken token)
        {
            if (option.Offset < 0)
                throw new ArgumentOutOfRangeException(nameof(option.Offset), option.Offset, "Offset must be greater than or equal to 0");

            if (option.MaxCount < 0)
                throw new ArgumentOutOfRangeException(nameof(option.MaxCount), option.MaxCount, "MaxCount must be greater than or equal to 0");
            
            await _semaphore.WaitAsync(token);

            
            try
            {
                if (token.IsCancellationRequested)
                    yield break;

                if (option.Keyword.StartsWith("@"))
                {
                    EverythingApiDllImport.Everything_SetRegex(true);
                    option.Keyword = option.Keyword[1..];
                }

                var builder = new StringBuilder();
                builder.Append(option.Keyword);

                if (!string.IsNullOrWhiteSpace(option.ParentPath))
                {
                    builder.Append($" {(option.IsRecursive ? "" : "parent:")}\"{option.ParentPath}\"");
                }

                if (option.IsContentSearch)
                {
                    builder.Append($" content:\"{option.ContentSearchKeyword}\"");
                }

                EverythingApiDllImport.Everything_SetSearchW(builder.ToString());
                EverythingApiDllImport.Everything_SetOffset(option.Offset);
                EverythingApiDllImport.Everything_SetMax(option.MaxCount);

                EverythingApiDllImport.Everything_SetSort(option.SortOption);
                EverythingApiDllImport.Everything_SetMatchPath(true);

                if (token.IsCancellationRequested) yield break;

                if (!EverythingApiDllImport.Everything_QueryW(true))
                {
                    CheckAndThrowExceptionOnError();
                    yield break;
                }

                for (var idx = 0; idx < EverythingApiDllImport.Everything_GetNumResults(); ++idx)
                {
                    if (token.IsCancellationRequested)
                    {
                        yield break;
                    }

                    EverythingApiDllImport.Everything_GetResultFullPathNameW(idx, buffer, BufferSize);

                    var result = new SearchResult
                    {
                        FullPath = buffer.ToString(),
                        Type = EverythingApiDllImport.Everything_IsFolderResult(idx) ? ResultType.Folder :
                            EverythingApiDllImport.Everything_IsFileResult(idx) ? ResultType.File :
                            ResultType.Volume
                    };

                    yield return result;
                }
            }
            finally
            {
                EverythingApiDllImport.Everything_Reset();
                _semaphore.Release();
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
                case StateCode.OK:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
