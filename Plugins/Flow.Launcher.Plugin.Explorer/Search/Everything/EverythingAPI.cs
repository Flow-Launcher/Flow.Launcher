using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Explorer.Exceptions;
using Flow.Launcher.Plugin.Explorer.Search.Everything.Exceptions;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    public class LegacyEverythingApi : IEverythingApi
    {
        private const int BufferSize = 4096;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);
        // cached buffer to remove redundant allocations.
        private static readonly StringBuilder buffer = new(BufferSize);

        const uint EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME = 0x00000004u;
        const uint EVERYTHING_REQUEST_RUN_COUNT = 0x00000400u;

        /// <summary>
        /// Checks whether the sort option is Fast Sort.
        /// </summary>
        public bool IsFastSortOption(EverythingSortOption sortOption)
        {
            var fastSortOptionEnabled = EverythingApiDllImport.Everything_IsFastSort(sortOption);
            // If the Everything service is not running, then this call will incorrectly report
            // the state as false. This checks for errors thrown by the api and up to the caller to handle.
            CheckAndThrowExceptionOnError();
            return fastSortOptionEnabled;
        }

        /// <summary>
        /// Searches using the specified criteria and resets the Everything API afterwards.
        /// </summary>
        /// <param name="option">The search criteria.</param>
        /// <param name="token">A cancellation token that stops the current search when cancellation is requested.</param>
        /// <returns>An asynchronous sequence of search results that match the specified criteria.</returns>
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
                _ = EverythingApiDllImport.Everything_GetMajorVersion();
                if (EverythingApiDllImport.Everything_GetLastError() == EverythingStateCode.IPCError)
                    throw new IPCErrorException();
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

                EverythingApiDllImport.Everything_SetRegex(preparedOption.UseRegex);
                EverythingApiDllImport.Everything_SetSearchW(query.SearchText);
                EverythingApiDllImport.Everything_SetOffset(preparedOption.Offset);
                EverythingApiDllImport.Everything_SetMax(preparedOption.MaxCount);

                EverythingApiDllImport.Everything_SetSort(preparedOption.SortOption);
                EverythingApiDllImport.Everything_SetMatchPath(preparedOption.IsFullPathSearch);

                if (preparedOption.SortOption == EverythingSortOption.RUN_COUNT_DESCENDING ||
                    preparedOption.SortOption == EverythingSortOption.RUN_COUNT_ASCENDING)
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
                        yield break;

                    EverythingApiDllImport.Everything_GetResultFullPathNameW(idx, buffer, BufferSize);

                    yield return new SearchResult
                    {
                        FullPath = buffer.ToString(),
                        Type = EverythingApiDllImport.Everything_IsFolderResult(idx) ? ResultType.Folder :
                            EverythingApiDllImport.Everything_IsFileResult(idx) ? ResultType.File :
                            ResultType.Volume,
                        Score = Convert.ToInt32(EverythingApiDllImport.Everything_GetResultRunCount((uint)idx)),
                        HighlightData = EverythingHelper.EverythingHighlightStringToHighlightList(EverythingApiDllImport.Everything_GetResultHighlightedFileName((uint)idx))
                    };
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
                case EverythingStateCode.CreateThreadError:
                    throw new CreateThreadException();
                case EverythingStateCode.CreateWindowError:
                    throw new CreateWindowException();
                case EverythingStateCode.InvalidCallError:
                    throw new InvalidCallException();
                case EverythingStateCode.InvalidIndexError:
                    throw new InvalidIndexException();
                case EverythingStateCode.IPCError:
                    throw new IPCErrorException();
                case EverythingStateCode.MemoryError:
                    throw new MemoryErrorException();
                case EverythingStateCode.RegisterClassExError:
                    throw new RegisterClassExException();
                case EverythingStateCode.OK:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
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
                _ = EverythingApiDllImport.Everything_IncRunCountFromFileName(fileOrFolder);
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
    }
}
