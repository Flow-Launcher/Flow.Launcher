using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Explorer.Exceptions;
using Flow.Launcher.Plugin.Explorer.Search.Everything.Exceptions;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    public class EverythingApiV3 : IEverythingApi
    {
        private const int BufferSize = 4096;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly StringBuilder _buffer = new(BufferSize);

        private readonly string _instanceName;
        private IntPtr _client;

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
        const uint EVERYTHING3_OK = 0;

        private void CheckEverything3Call(string callName, bool succeeded)
        {
            if (!succeeded)
            {
                Main.Context?.API?.LogDebug(nameof(EverythingApiV3), $"{callName} failed");
                CheckAndThrowExceptionOnErrorFromEverything3();
            }
        }

        public async IAsyncEnumerable<SearchResult> SearchAsync(EverythingSearchOption option, [EnumeratorCancellation] CancellationToken token = default)
        {
            await foreach (var result in SearchCoreAsync(option, token))
                yield return result;
        }

        public async Task CheckAvailableAsync(CancellationToken token = default)
        {
            try
            {
                await _semaphore.WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                token.ThrowIfCancellationRequested();

                if (!EverythingClientConnected())
                {
                    _client = IntPtr.Zero;
                    throw new IPCErrorException();
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async IAsyncEnumerable<SearchResult> SearchCoreAsync(EverythingSearchOption option, [EnumeratorCancellation] CancellationToken token)
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

                if (!EverythingClientConnected())
                    throw new IPCErrorException();

                await foreach (var result in SearchWithEverything3Async(preparedOption, query, token))
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
                if (EverythingClientConnected())
                {
                    try
                    {
                        var incremented = Everything3ApiDllImport.Everything3_IncRunCountFromFilenameW(_client, fileOrFolder) != 0;
                        CheckEverything3Call(nameof(Everything3ApiDllImport.Everything3_IncRunCountFromFilenameW), incremented);
                    }
                    catch (IPCErrorException)
                    {
                        DestroyEverythingClient(_client);
                        throw;
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
            if (!_semaphore.Wait(TimeSpan.FromSeconds(1)))
                return false;

            try
            {
                if (!EverythingClientConnected())
                    throw new IPCErrorException();

                if (TryConvertSortOption(sortOption, out var propertyId, out _))
                {
                    var isFastSort = Everything3ApiDllImport.Everything3_IsPropertyFastSort(_client, propertyId);
                    CheckAndThrowExceptionOnErrorFromEverything3();
                    return isFastSort;
                }
            }
            catch (IPCErrorException)
            {
                DestroyEverythingClient(_client);
                throw;
            }
            finally
            {
                _semaphore.Release();
            }

            return false;
        }

        private async IAsyncEnumerable<SearchResult> SearchWithEverything3Async(
            EverythingSearchOption option,
            EverythingHelper.PreparedQuery query,
            [EnumeratorCancellation] CancellationToken token)
        {
            IntPtr searchState = IntPtr.Zero;
            IntPtr resultList = IntPtr.Zero;
            var completed = false;
            var includeRunCount = option.IsRunCounterEnabled || option.SortOption == EverythingSortOption.RUN_COUNT_DESCENDING || option.SortOption == EverythingSortOption.RUN_COUNT_ASCENDING;

            try
            {
                if (token.IsCancellationRequested)
                    yield break;
                searchState = Everything3ApiDllImport.Everything3_CreateSearchState();
                if (searchState == IntPtr.Zero)
                {
                    CheckAndThrowExceptionOnErrorFromEverything3();
                    yield break;
                }

                CheckEverything3Call(nameof(Everything3ApiDllImport.Everything3_SetSearchRegex),
                    Everything3ApiDllImport.Everything3_SetSearchRegex(searchState, option.UseRegex));
                CheckEverything3Call(nameof(Everything3ApiDllImport.Everything3_SetSearchMatchPath),
                    Everything3ApiDllImport.Everything3_SetSearchMatchPath(searchState, option.IsFullPathSearch));
                CheckEverything3Call(nameof(Everything3ApiDllImport.Everything3_SetSearchTextW),
                    Everything3ApiDllImport.Everything3_SetSearchTextW(searchState, query.SearchText));
                CheckEverything3Call(nameof(Everything3ApiDllImport.Everything3_SetSearchHideResultOmissions),
                    Everything3ApiDllImport.Everything3_SetSearchHideResultOmissions(searchState, true));
                CheckEverything3Call(nameof(Everything3ApiDllImport.Everything3_SetSearchViewportOffset),
                    Everything3ApiDllImport.Everything3_SetSearchViewportOffset(searchState, (nuint)option.Offset));
                CheckEverything3Call(nameof(Everything3ApiDllImport.Everything3_SetSearchViewportCount),
                    Everything3ApiDllImport.Everything3_SetSearchViewportCount(searchState, (nuint)option.MaxCount));

                if (TryConvertSortOption(option.SortOption, out var sortPropertyId, out var ascending))
                {
                    if (!Everything3ApiDllImport.Everything3_AddSearchSort(searchState, sortPropertyId, ascending))
                    {
                        CheckEverything3Call(nameof(Everything3ApiDllImport.Everything3_AddSearchSort), false);
                    }
                }

                CheckEverything3Call(nameof(Everything3ApiDllImport.Everything3_ClearSearchPropertyRequests),
                    Everything3ApiDllImport.Everything3_ClearSearchPropertyRequests(searchState));
                CheckEverything3Call(nameof(Everything3ApiDllImport.Everything3_AddSearchPropertyRequestHighlighted),
                    Everything3ApiDllImport.Everything3_AddSearchPropertyRequestHighlighted(searchState, EVERYTHING3_PROPERTY_ID_NAME));
                CheckEverything3Call(nameof(Everything3ApiDllImport.Everything3_AddSearchPropertyRequest),
                    Everything3ApiDllImport.Everything3_AddSearchPropertyRequest(searchState, EVERYTHING3_PROPERTY_ID_PATH_AND_NAME));
                if (includeRunCount)
                {
                    CheckEverything3Call(nameof(Everything3ApiDllImport.Everything3_AddSearchPropertyRequest),
                        Everything3ApiDllImport.Everything3_AddSearchPropertyRequest(searchState, EVERYTHING3_PROPERTY_ID_RUN_COUNT));
                }
                if (token.IsCancellationRequested)
                    yield break;
                resultList = Everything3ApiDllImport.Everything3_Search(_client, searchState);
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

                    if (!TryCreateSearchResult(resultList, idx, includeRunCount, out var result))
                        continue;

                    yield return result;
                }

                completed = true;
            }
            finally
            {
                if (resultList != IntPtr.Zero)
                    _ = Everything3ApiDllImport.Everything3_DestroyResultList(resultList);

                if (searchState != IntPtr.Zero)
                    _ = Everything3ApiDllImport.Everything3_DestroySearchState(searchState);

                if (!completed)
                    DestroyEverythingClient(_client);
            }

            await Task.CompletedTask;
        }

        private bool TryCreateSearchResult(IntPtr resultList, nuint resultIndex, bool includeRunCount, out SearchResult result)
        {
            result = default;

            if (!TryGetResultFullPath(resultList, resultIndex, out var fullPath))
                return false;

            result = new SearchResult
            {
                FullPath = fullPath,
                Type = GetResultType(resultList, resultIndex),
                Score = includeRunCount ? GetResultScore(resultList, resultIndex) : 0,
                HighlightData = GetHighlightData(resultList, resultIndex)
            };

            return true;
        }

        private int GetResultScore(IntPtr resultList, nuint resultIndex)
        {
            var runCount = Everything3ApiDllImport.Everything3_GetResultRunCount(resultList, resultIndex);
            var lastError = Everything3ApiDllImport.Everything3_GetLastError();

            // if there is any error then set score to zero (this also covers PROPERTY_NOT_FOUND when run count is not requested)
            if (lastError != EVERYTHING3_OK)
            {
                Main.Context?.API?.LogDebug(nameof(EverythingApiV3), $"{nameof(Everything3ApiDllImport.Everything3_GetResultRunCount)} failed with error 0x{lastError:X8}");
                return 0;
            }

            // if genuinely a value too large for int then clamp it
            if (runCount > int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)runCount;
        }

        private bool TryGetResultFullPath(IntPtr resultList, nuint resultIndex, out string fullPath)
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

        private ResultType GetResultType(IntPtr resultList, nuint resultIndex)
        {
            return Everything3ApiDllImport.Everything3_IsFolderResult(resultList, resultIndex)
                ? ResultType.Folder
                : Everything3ApiDllImport.Everything3_IsRootResult(resultList, resultIndex)
                    ? ResultType.Volume
                    : ResultType.File;
        }

        private List<int> GetHighlightData(IntPtr resultList, nuint resultIndex)
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

        private bool EverythingClientConnected()
        {
            if (_client == IntPtr.Zero)
                _client = Everything3ApiDllImport.Everything3_ConnectW(_instanceName);

            if (_client == IntPtr.Zero || Everything3ApiDllImport.Everything3_GetMajorVersion(_client) == 0)
            {
                DestroyEverythingClient(_client);
                _client = Everything3ApiDllImport.Everything3_ConnectW(_instanceName);
            }

            return _client != IntPtr.Zero;
        }

        private void DestroyEverythingClient(IntPtr client)
        {
            if (client == IntPtr.Zero || client != _client)
                return;

            _ = Everything3ApiDllImport.Everything3_DestroyClient(_client);
            _client = IntPtr.Zero;
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

        private void CheckAndThrowExceptionOnErrorFromEverything3()
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
                case EVERYTHING3_OK:
                    return;
                default:
                    throw new InvalidCallException();
            }
        }
    }
}
