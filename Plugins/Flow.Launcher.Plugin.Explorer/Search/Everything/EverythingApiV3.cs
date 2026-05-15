using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Explorer.Search.Everything.Exceptions;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    public class EverythingApiV3 : IEverythingApi
    {
        private const int BufferSize = 4096;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);
        private static readonly StringBuilder _buffer = new(BufferSize);

        private readonly string _instanceName;

        public EverythingApiV3(string instanceName)
        {
            _instanceName = instanceName;
        }

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

        public async ValueTask<bool> IsEverythingRunningAsync(CancellationToken token = default)
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
                if (!TryConnectEverything3(out var client))
                    return false;

                _ = Everything3ApiDllImport.Everything3_DestroyClient(client);
                return true;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async IAsyncEnumerable<SearchResult> SearchAsync(EverythingSearchOption option, [EnumeratorCancellation] CancellationToken token = default)
        {
            var query = EverythingHelper.PrepareQuery(option);
            var preparedOption = query.Option;

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

                if (!TryConnectEverything3(out var client))
                    throw new IPCErrorException();

                await foreach (var result in SearchWithEverything3Async(client, preparedOption, query, token))
                    yield return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task IncrementRunCounterAsync(string fileOrFolder)
        {
            var _entered = await _semaphore.WaitAsync(TimeSpan.FromSeconds(1));
            if (!_entered)
            {
                // If we can't acquire the semaphore within the timeout, we skip incrementing the run count to avoid blocking.
                return;
            }
            try
            {
                if (TryConnectEverything3(out var client))
                {
                    try
                    {
                        Everything3ApiDllImport.Everything3_IncRunCountFromFilenameW(client, fileOrFolder);
                    }
                    finally
                    {
                        _ = Everything3ApiDllImport.Everything3_DestroyClient(client);
                    }
                }
            }
            catch (Exception)
            {
                /*ignored*/
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public bool IsFastSortOption(EverythingSortOption sortOption)
        {
            if (!TryConnectEverything3(out var client))
                throw new IPCErrorException();

            try
            {
                if (TryConvertSortOption(sortOption, out var propertyId, out _))
                {
                    var isFastSort = Everything3ApiDllImport.Everything3_IsPropertyFastSort(client, propertyId);
                    CheckAndThrowExceptionOnErrorFromEverything3();
                    return isFastSort;
                }
            }
            finally
            {
                _ = Everything3ApiDllImport.Everything3_DestroyClient(client);
                CheckAndThrowExceptionOnErrorFromEverything3();
            }

            return true;
        }

        private static async IAsyncEnumerable<SearchResult> SearchWithEverything3Async(IntPtr client,
            EverythingSearchOption option,
            EverythingHelper.PreparedQuery query,
            [EnumeratorCancellation] CancellationToken token)
        {
            IntPtr searchState = IntPtr.Zero;
            IntPtr resultList = IntPtr.Zero;
            var includeRunCount = option.IsRunCounterEnabled || option.SortOption == EverythingSortOption.RUN_COUNT_DESCENDING || option.SortOption == EverythingSortOption.RUN_COUNT_ASCENDING;

            try
            {
                searchState = Everything3ApiDllImport.Everything3_CreateSearchState();
                if (searchState == IntPtr.Zero)
                {
                    CheckAndThrowExceptionOnErrorFromEverything3();
                    yield break;
                }

                _ = Everything3ApiDllImport.Everything3_SetSearchRegex(searchState, option.UseRegex);
                _ = Everything3ApiDllImport.Everything3_SetSearchMatchPath(searchState, option.IsFullPathSearch);
                _ = Everything3ApiDllImport.Everything3_SetSearchTextW(searchState, query.SearchText);
                _ = Everything3ApiDllImport.Everything3_SetSearchHideResultOmissions(searchState, true);
                _ = Everything3ApiDllImport.Everything3_SetSearchViewportOffset(searchState, (nuint)option.Offset);
                _ = Everything3ApiDllImport.Everything3_SetSearchViewportCount(searchState, (nuint)option.MaxCount);

                if (TryConvertSortOption(option.SortOption, out var sortPropertyId, out var ascending))
                {
                    if (!Everything3ApiDllImport.Everything3_AddSearchSort(searchState, sortPropertyId, ascending))
                    {
                        CheckAndThrowExceptionOnErrorFromEverything3();
                        yield break;
                    }
                }

                _ = Everything3ApiDllImport.Everything3_ClearSearchPropertyRequests(searchState);
                _ = Everything3ApiDllImport.Everything3_AddSearchPropertyRequestHighlighted(searchState, EVERYTHING3_PROPERTY_ID_NAME);
                //_ = Everything3ApiDllImport.Everything3_AddSearchPropertyRequestHighlighted(searchState, EVERYTHING3_PROPERTY_ID_PATH);
                _ = Everything3ApiDllImport.Everything3_AddSearchPropertyRequest(searchState, EVERYTHING3_PROPERTY_ID_PATH_AND_NAME);
                if (includeRunCount)
                    _ = Everything3ApiDllImport.Everything3_AddSearchPropertyRequest(searchState, EVERYTHING3_PROPERTY_ID_RUN_COUNT);

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

                    if (!TryCreateSearchResult(resultList, idx, out var result))
                        continue;

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
        }

        private static bool TryCreateSearchResult(IntPtr resultList, nuint resultIndex, out SearchResult result)
        {
            result = default;

            if (!TryGetResultFullPath(resultList, resultIndex, out var fullPath))
                return false;

            result = new SearchResult
            {
                FullPath = fullPath,
                Type = GetResultType(resultList, resultIndex),
                Score = Convert.ToInt32(Everything3ApiDllImport.Everything3_GetResultRunCount(resultList, resultIndex)),
                HighlightData = GetHighlightData(resultList, resultIndex)
            };

            return true;
        }

        private static bool TryGetResultFullPath(IntPtr resultList, nuint resultIndex, out string fullPath)
        {
            _buffer.Clear();
            var fullPathLength = Everything3ApiDllImport.Everything3_GetResultFullPathNameW(resultList, resultIndex, _buffer, BufferSize);
            if (fullPathLength == 0)
            {
                CheckAndThrowExceptionOnErrorFromEverything3();
                fullPath = string.Empty;
                return false;
            }

            fullPath = _buffer.ToString();
            return !string.IsNullOrEmpty(fullPath);
        }

        private static ResultType GetResultType(IntPtr resultList, nuint resultIndex)
        {
            return Everything3ApiDllImport.Everything3_IsFolderResult(resultList, resultIndex)
                ? ResultType.Folder
                : Everything3ApiDllImport.Everything3_IsRootResult(resultList, resultIndex)
                    ? ResultType.Volume
                    : ResultType.File;
        }

        private static List<int> GetHighlightData(IntPtr resultList, nuint resultIndex)
        {
            _buffer.Clear();
            var highlightedFileNameLength = Everything3ApiDllImport.Everything3_GetResultPropertyTextHighlightedW(
                resultList,
                resultIndex,
                EVERYTHING3_PROPERTY_ID_NAME,
                _buffer,
                BufferSize);

            return highlightedFileNameLength > 0
                ? EverythingHelper.EverythingHighlightStringToHighlightList(_buffer.ToString())
                : [];
        }

        private bool TryUseEverything3Client(Action<IntPtr> action)
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

        private bool TryConnectEverything3(out IntPtr client)
        {
            client = Everything3ApiDllImport.Everything3_ConnectW(_instanceName);
            return client != IntPtr.Zero;
        }

        /// <summary>
        /// Covert the old Everything 1.4 sort options in our UI to Everything 3 property ID and sort direction for compatibility.
        /// </summary>
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
                case EverythingSortOption.RUN_COUNT_ASCENDING:
                    propertyId = EVERYTHING3_PROPERTY_ID_RUN_COUNT;
                    ascending = true;
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
    }
}
