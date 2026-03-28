using Flow.Launcher.Plugin.Explorer.Search.Everything.Exceptions;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    public static partial class EverythingApi
    {
        public const string DefaultEverything15InstanceName = "1.5a";

        private const int BufferSize = 4096;

        private static readonly SemaphoreSlim _semaphore = new(1, 1);
        private static volatile bool _enableEverything15Support = true;
        private static string _everything15InstanceName = DefaultEverything15InstanceName;

        // cached buffer to remove redundant allocations. semaphore is used to make sure the access to the buffer is thread safe.
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

        const uint EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME = 0x00000004u;
        const uint EVERYTHING_REQUEST_RUN_COUNT = 0x00000400u;

        public static bool EnableEverything15Support => _enableEverything15Support;
        public static string Everything15InstanceName => _everything15InstanceName;

        public static void ConfigureEverythingSupport(bool enableEverything15Support, string sdkDirectory, string everything15InstanceName)
        {
            _semaphore.Wait();
            try
            {
                var normalizedInstanceName = string.IsNullOrWhiteSpace(everything15InstanceName)
                    ? DefaultEverything15InstanceName
                    : everything15InstanceName.Trim();

                var supportChanged = _enableEverything15Support != enableEverything15Support;
                _enableEverything15Support = enableEverything15Support;
                _everything15InstanceName = normalizedInstanceName;

                if (enableEverything15Support)
                {
                    if (supportChanged || !Everything3ApiDllImport.IsLoaded)
                    {
                        if (EverythingApiDllImport.IsLoaded)
                            EverythingApiDllImport.Unload();

                        Everything3ApiDllImport.Load(sdkDirectory);
                    }
                }
                else
                {
                    if (supportChanged || !EverythingApiDllImport.IsLoaded)
                    {
                        if (Everything3ApiDllImport.IsLoaded)
                            Everything3ApiDllImport.Unload();

                        EverythingApiDllImport.Load(sdkDirectory);
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Checks whether the sort option is Fast Sort.
        /// </summary>
        public static bool IsFastSortOption(EverythingSortOption sortOption)
        {
            if (EnableEverything15Support)
            {
                if (!TryConnectEverything3(out var client))
                    throw new IPCErrorException();

                try
                {
                    if (TryConvertSortOption(sortOption, out var propertyId, out _))
                    {
                        var isFastSort = Everything3ApiDllImport.Everything3_IsPropertyFastSort(client, propertyId);

                        // Keep the same behavior as legacy path: throw when engine is not available.
                        CheckAndThrowExceptionOnErrorFromEverything3();

                        return isFastSort;
                    }
                }
                finally
                {
                    _ = Everything3ApiDllImport.Everything3_DestroyClient(client);
                    // Throw again to the caller
                    CheckAndThrowExceptionOnErrorFromEverything3();
                }

                return true;
            }

            var fastSortOptionEnabled = EverythingApiDllImport.Everything_IsFastSort(sortOption);

            // If the Everything service is not running, then this call will incorrectly report 
            // the state as false. This checks for errors thrown by the api and up to the caller to handle.
            CheckAndThrowExceptionOnError();

            return fastSortOptionEnabled;
        }

        public static async ValueTask<bool> IsEverythingRunningAsync(CancellationToken token = default)
        {
            try
            {
                await _semaphore.WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            try
            {
                if (EnableEverything15Support)
                    return TryUseEverything3Client(static _ => { });

                _ = EverythingApiDllImport.Everything_GetMajorVersion();
                return EverythingApiDllImport.Everything_GetLastError() != StateCode.IPCError;
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

            try
            {
                await _semaphore.WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            try
            {
                if (token.IsCancellationRequested)
                    yield break;

                var useRegex = false;
                if (option.Keyword.StartsWith("@"))
                {
                    useRegex = true;
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

                var searchText = builder.ToString();

                if (EnableEverything15Support)
                {
                    if (!TryConnectEverything3(out var client))
                        throw new IPCErrorException();

                    await foreach (var result in SearchWithEverything3Async(client, option, searchText, useRegex, token))
                        yield return result;

                    yield break;
                }

                await foreach (var result in SearchWithEverythingLegacyAsync(option, searchText, useRegex, token))
                    yield return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private static async IAsyncEnumerable<SearchResult> SearchWithEverythingLegacyAsync(EverythingSearchOption option,
            string searchText,
            bool useRegex,
            [EnumeratorCancellation] CancellationToken token)
        {
            try
            {
                EverythingApiDllImport.Everything_SetRegex(useRegex);
                EverythingApiDllImport.Everything_SetSearchW(searchText);
                EverythingApiDllImport.Everything_SetOffset(option.Offset);
                EverythingApiDllImport.Everything_SetMax(option.MaxCount);

                EverythingApiDllImport.Everything_SetSort(option.SortOption);
                EverythingApiDllImport.Everything_SetMatchPath(option.IsFullPathSearch);

                if (option.SortOption == EverythingSortOption.RUN_COUNT_DESCENDING)
                {
                    EverythingApiDllImport.Everything_SetRequestFlags(EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME | EVERYTHING_REQUEST_RUN_COUNT);
                }
                else
                {
                    EverythingApiDllImport.Everything_SetRequestFlags(EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME);
                }

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
                            ResultType.Volume,
                        Score = Convert.ToInt32(EverythingApiDllImport.Everything_GetResultRunCount((uint)idx)),
                        HighlightData = EverythingHighlightStringToHighlightList(EverythingApiDllImport.Everything_GetResultHighlightedFileName((uint)idx))
                    };

                    yield return result;
                }
            }
            finally
            {
                EverythingApiDllImport.Everything_Reset();
            }

            await Task.CompletedTask;
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

        public static async Task IncrementRunCounterAsync(string fileOrFolder)
        {
            await _semaphore.WaitAsync(TimeSpan.FromSeconds(1));
            try
            {
                if (EnableEverything15Support)
                {
                    _ = TryUseEverything3Client(client =>
                        _ = Everything3ApiDllImport.Everything3_IncRunCountFromFilenameW(client, fileOrFolder));
                    return;
                }

                _ = EverythingApiDllImport.Everything_IncRunCountFromFileName(fileOrFolder);
            }
            catch (Exception)
            {
                /*ignored*/
            }
            finally { _semaphore.Release(); }
        }

        /// <summary>
        /// Convert the highlighted string from Everything API to a list of highlight indexes for our Result.
        /// </summary>
        /// <param name="highlightString">Text inside a * quote is highlighted, two consecutive *'s is a single literal *. For example, in the highlighted text: abc*123* the 123 part is highlighted.</param>
        /// <returns>A list of zero-based character indices that should be highlighted.</returns>
        public static List<int> EverythingHighlightStringToHighlightList(string highlightString)
        {
            var highlightData = new List<int>();

            if (string.IsNullOrEmpty(highlightString))
                return highlightData;

            var isHighlighted = false;
            var actualIndex = 0; // Index in the actual string (without * markers)
            var length = highlightString.Length;

            for (var i = 0; i < length; i++)
            {
                if (highlightString[i] == '*')
                {
                    // Check if it's a literal * (two consecutive *)
                    if (i + 1 < length && highlightString[i + 1] == '*')
                    {
                        // Two consecutive *'s represent a single literal *
                        if (isHighlighted)
                        {
                            highlightData.Add(actualIndex);
                        }
                        actualIndex++;
                        i++; // Skip the next *
                    }
                    else
                    {
                        isHighlighted = !isHighlighted;
                    }
                }
                else
                {
                    // Regular character
                    if (isHighlighted)
                    {
                        highlightData.Add(actualIndex);
                    }
                    actualIndex++;
                }
            }

            return highlightData;
        }
    }
}
