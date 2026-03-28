using Flow.Launcher.Plugin.Explorer.Search.Everything.Exceptions;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    public static partial class EverythingApi
    {
        private const string Everything15AlphaInstance = "1.5a";

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
                _ = Everything3ApiDllImport.Everything3_AddSearchPropertyRequestHighlighted(searchState, EVERYTHING3_PROPERTY_ID_NAME);
                _ = Everything3ApiDllImport.Everything3_AddSearchPropertyRequestHighlighted(searchState, EVERYTHING3_PROPERTY_ID_PATH);
                // Everything3_GetResultFullPathNameW requires PATH_AND_NAME to be requested
                _ = Everything3ApiDllImport.Everything3_AddSearchPropertyRequest(searchState, EVERYTHING3_PROPERTY_ID_PATH_AND_NAME);
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

            if (!EnableEverything15Support)
                return false;

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
    }
}
