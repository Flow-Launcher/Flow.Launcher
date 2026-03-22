using Flow.Launcher.Plugin.Explorer.Search.Everything.Exceptions;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    public static class EverythingApi
    {
        private const int BufferSize = 4096;
        private const string Everything15AlphaInstance = "1.5a";

        private static readonly SemaphoreSlim _semaphore = new(1, 1);

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

        const uint EVERYTHING3_PROPERTY_ID_NAME = 0;
        const uint EVERYTHING3_PROPERTY_ID_PATH = 1;
        const uint EVERYTHING3_PROPERTY_ID_SIZE = 2;
        const uint EVERYTHING3_PROPERTY_ID_EXTENSION = 3;
        const uint EVERYTHING3_PROPERTY_ID_TYPE = 4;
        const uint EVERYTHING3_PROPERTY_ID_DATE_MODIFIED = 5;
        const uint EVERYTHING3_PROPERTY_ID_DATE_CREATED = 6;
        const uint EVERYTHING3_PROPERTY_ID_DATE_ACCESSED = 7;
        const uint EVERYTHING3_PROPERTY_ID_ATTRIBUTES = 8;
        const uint EVERYTHING3_PROPERTY_ID_DATE_RECENTLY_CHANGED = 9;
        const uint EVERYTHING3_PROPERTY_ID_RUN_COUNT = 10;
        const uint EVERYTHING3_PROPERTY_ID_DATE_RUN = 11;
        const uint EVERYTHING3_PROPERTY_ID_FILE_LIST_NAME = 12;
        const uint EVERYTHING3_PROPERTY_ID_PATH_AND_NAME = 240;

        const uint EVERYTHING3_ERROR_OUT_OF_MEMORY = 0xE0000001;
        const uint EVERYTHING3_ERROR_IPC_PIPE_NOT_FOUND = 0xE0000002;
        const uint EVERYTHING3_ERROR_DISCONNECTED = 0xE0000003;
        const uint EVERYTHING3_ERROR_INVALID_PARAMETER = 0xE0000004;
        const uint EVERYTHING3_ERROR_PROPERTY_NOT_FOUND = 0xE0000007;

        /// <summary>
        /// Checks whether the sort option is Fast Sort.
        /// </summary>
        public static bool IsFastSortOption(EverythingSortOption sortOption)
        {
            if (TryConnectEverything3(out var client))
            {
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
                if (TryUseEverything3Client(static _ => { }))
                    return true;

                _ = EverythingApiDllImport.Everything_GetMajorVersion();
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

                if (TryConnectEverything3(out var client))
                {
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

        private static async IAsyncEnumerable<SearchResult> SearchWithEverything3Async(IntPtr client,
            EverythingSearchOption option,
            string searchText,
            bool useRegex,
            [EnumeratorCancellation] CancellationToken token)
        {
            IntPtr searchState = IntPtr.Zero;
            IntPtr resultList = IntPtr.Zero;
            try
            {
                searchState = Everything3ApiDllImport.Everything3_CreateSearchState();
                if (searchState == IntPtr.Zero)
                {
                    CheckAndThrowExceptionOnErrorFromEverything3();
                    yield break;
                }

                _ = Everything3ApiDllImport.Everything3_SetSearchRegex(searchState, useRegex);
                _ = Everything3ApiDllImport.Everything3_SetSearchMatchPath(searchState, option.IsFullPathSearch);
                _ = Everything3ApiDllImport.Everything3_SetSearchTextW(searchState, searchText);
                _ = Everything3ApiDllImport.Everything3_SetSearchHideResultOmissions(searchState, true);
                _ = Everything3ApiDllImport.Everything3_SetSearchViewportOffset(searchState, (nuint)option.Offset);
                _ = Everything3ApiDllImport.Everything3_SetSearchViewportCount(searchState, (nuint)option.MaxCount);

                if (TryConvertSortOption(option.SortOption, out var sortPropertyId, out var ascending))
                {
                    _ = Everything3ApiDllImport.Everything3_AddSearchSort(searchState, sortPropertyId, ascending);
                }

                _ = Everything3ApiDllImport.Everything3_ClearSearchPropertyRequests(searchState);
                // TODO somehow error, no result
                _ = Everything3ApiDllImport.Everything3_AddSearchPropertyRequestHighlighted(searchState, EVERYTHING3_PROPERTY_ID_NAME);
                // TODO need to check the "IsFullPathSearch"
                _ = Everything3ApiDllImport.Everything3_AddSearchPropertyRequestHighlighted(searchState, EVERYTHING3_PROPERTY_ID_PATH);

                if (token.IsCancellationRequested)
                    yield break;

                resultList = Everything3ApiDllImport.Everything3_Search(client, searchState);
                if (resultList == IntPtr.Zero)
                {
                    CheckAndThrowExceptionOnErrorFromEverything3();
                    yield break;
                }

                var resultCount = Everything3ApiDllImport.Everything3_GetResultListViewportCount(resultList);
                for (nuint idx = 0; idx < resultCount; ++idx)
                {
                    if (token.IsCancellationRequested)
                    {
                        yield break;
                    }

                    buffer.Clear();
                    var fullPathLength = Everything3ApiDllImport.Everything3_GetResultFullPathNameW(resultList, idx, buffer, BufferSize);
                    if (fullPathLength == 0)
                    {
                        CheckAndThrowExceptionOnErrorFromEverything3();
                        continue;
                    }

                    var fullPath = buffer.ToString();
                    if (string.IsNullOrEmpty(fullPath))
                    {
                        continue;
                    }

                    var result = new SearchResult
                    {
                        FullPath = fullPath,
                        Type = Everything3ApiDllImport.Everything3_IsFolderResult(resultList, idx)
                            ? ResultType.Folder
                            : Everything3ApiDllImport.Everything3_IsRootResult(resultList, idx)
                                ? ResultType.Volume
                                : ResultType.File,
                        Score = Convert.ToInt32(Everything3ApiDllImport.Everything3_GetResultRunCount(resultList, idx))
                    };

                    // 0 for the first requested property, which is name in our case.
                    if (Everything3ApiDllImport.Everything3_GetSearchPropertyRequestHighlight(searchState, 0))
                    {
                        buffer.Clear();
                        var highlightedFileNameLength = Everything3ApiDllImport.Everything3_GetResultPropertyTextHighlightedW(
                            resultList,
                            idx,
                            EVERYTHING3_PROPERTY_ID_NAME,
                            buffer,
                            BufferSize);

                        if (highlightedFileNameLength > 0)
                        {
                            var highlightData = EverythingHighlightStringToHighlightList(buffer.ToString());
                            if (highlightData.Count > 0)
                            {
                                result = result with
                                {
                                    HighlightData = highlightData
                                };
                            }
                        }
                    }

                    yield return result;
                }
            }
            finally
            {
                if (resultList != IntPtr.Zero)
                    _ = Everything3ApiDllImport.Everything3_DestroyResultList(resultList);

                if (searchState != IntPtr.Zero)
                    _ = Everything3ApiDllImport.Everything3_DestroySearchState(searchState);

                _ = Everything3ApiDllImport.Everything3_DestroyClient(client);
            }

            await Task.CompletedTask;
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

        private static bool TryUseEverything3Client(Action<IntPtr> action)
        {
            if (!TryConnectEverything3(out var client))
                return false;

            try
            {
                action(client);
                return true;
            }
            finally
            {
                _ = Everything3ApiDllImport.Everything3_DestroyClient(client);
            }
        }

        private static bool TryConnectEverything3(out IntPtr client)
        {
            client = IntPtr.Zero;
            try
            {
                client = Everything3ApiDllImport.Everything3_ConnectW(Everything15AlphaInstance);
                return client != IntPtr.Zero;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        private static bool TryConvertSortOption(EverythingSortOption sortOption, out uint propertyId, out bool ascending)
        {
            switch (sortOption)
            {
                case EverythingSortOption.NAME_ASCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_NAME;
                    ascending = true;
                    return true;
                case EverythingSortOption.NAME_DESCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_NAME;
                    ascending = false;
                    return true;
                case EverythingSortOption.PATH_ASCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_PATH;
                    ascending = true;
                    return true;
                case EverythingSortOption.PATH_DESCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_PATH;
                    ascending = false;
                    return true;
                case EverythingSortOption.SIZE_ASCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_SIZE;
                    ascending = true;
                    return true;
                case EverythingSortOption.SIZE_DESCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_SIZE;
                    ascending = false;
                    return true;
                case EverythingSortOption.EXTENSION_ASCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_EXTENSION;
                    ascending = true;
                    return true;
                case EverythingSortOption.EXTENSION_DESCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_EXTENSION;
                    ascending = false;
                    return true;
                case EverythingSortOption.TYPE_NAME_ASCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_TYPE;
                    ascending = true;
                    return true;
                case EverythingSortOption.TYPE_NAME_DESCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_TYPE;
                    ascending = false;
                    return true;
                case EverythingSortOption.DATE_CREATED_ASCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_DATE_CREATED;
                    ascending = true;
                    return true;
                case EverythingSortOption.DATE_CREATED_DESCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_DATE_CREATED;
                    ascending = false;
                    return true;
                case EverythingSortOption.DATE_MODIFIED_ASCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_DATE_MODIFIED;
                    ascending = true;
                    return true;
                case EverythingSortOption.DATE_MODIFIED_DESCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_DATE_MODIFIED;
                    ascending = false;
                    return true;
                case EverythingSortOption.ATTRIBUTES_ASCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_ATTRIBUTES;
                    ascending = true;
                    return true;
                case EverythingSortOption.ATTRIBUTES_DESCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_ATTRIBUTES;
                    ascending = false;
                    return true;
                case EverythingSortOption.FILE_LIST_FILENAME_ASCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_FILE_LIST_NAME;
                    ascending = true;
                    return true;
                case EverythingSortOption.FILE_LIST_FILENAME_DESCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_FILE_LIST_NAME;
                    ascending = false;
                    return true;
                case EverythingSortOption.RUN_COUNT_DESCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_RUN_COUNT;
                    ascending = false;
                    return true;
                case EverythingSortOption.DATE_RECENTLY_CHANGED_ASCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_DATE_RECENTLY_CHANGED;
                    ascending = true;
                    return true;
                case EverythingSortOption.DATE_RECENTLY_CHANGED_DESCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_DATE_RECENTLY_CHANGED;
                    ascending = false;
                    return true;
                case EverythingSortOption.DATE_ACCESSED_ASCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_DATE_ACCESSED;
                    ascending = true;
                    return true;
                case EverythingSortOption.DATE_ACCESSED_DESCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_DATE_ACCESSED;
                    ascending = false;
                    return true;
                case EverythingSortOption.DATE_RUN_ASCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_DATE_RUN;
                    ascending = true;
                    return true;
                case EverythingSortOption.DATE_RUN_DESCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_DATE_RUN;
                    ascending = false;
                    return true;
                default:
                    propertyId = EVERYTHING3_PROPERTY_ID_NAME;
                    ascending = true;
                    return false;
            }
        }

        private static void CheckAndThrowExceptionOnErrorFromEverything3()
        {
            switch (Everything3ApiDllImport.Everything3_GetLastError())
            {
                case EVERYTHING3_ERROR_OUT_OF_MEMORY:
                    throw new MemoryErrorException();
                case EVERYTHING3_ERROR_IPC_PIPE_NOT_FOUND:
                case EVERYTHING3_ERROR_DISCONNECTED:
                    throw new IPCErrorException();
                case EVERYTHING3_ERROR_INVALID_PARAMETER:
                    throw new InvalidCallException();
                case EVERYTHING3_ERROR_PROPERTY_NOT_FOUND:
                    throw new ArgumentException("EVERYTHING3_ERROR_PROPERTY_NOT_FOUND");
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

        public static async Task IncrementRunCounterAsync(string fileOrFolder)
        {
            await _semaphore.WaitAsync(TimeSpan.FromSeconds(1));
            try
            {
                if (TryUseEverything3Client(client =>
                    _ = Everything3ApiDllImport.Everything3_IncRunCountFromFilenameW(client, fileOrFolder)))
                    return;

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
