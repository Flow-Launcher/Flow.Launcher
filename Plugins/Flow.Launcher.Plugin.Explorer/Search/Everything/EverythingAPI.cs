using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
                _ = EverythingApiDllImport.Everything_GetMajorVersion();
                return EverythingApiDllImport.Everything_GetLastError() != EverythingHelper.StateCode.IPCError;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Searches using the specified criteria and resets the Everything API afterwards.
        /// </summary>
        /// <param name="option">The search criteria.</param>
        /// <param name="token">A cancellation token that stops the current search when cancellation is requested.</param>
        /// <returns>An asynchronous sequence of search results that match the specified criteria.</returns>
        public async IAsyncEnumerable<SearchResult> SearchAsync(EverythingSearchOption option, [EnumeratorCancellation] CancellationToken token = default)
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

                EverythingApiDllImport.Everything_SetRegex(useRegex);
                EverythingApiDllImport.Everything_SetSearchW(builder.ToString());
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
                        HighlightData = EverythingHelper.EverythingHighlightStringToHighlightList(EverythingApiDllImport.Everything_GetResultHighlightedFileName((uint)idx))
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
                case EverythingHelper.StateCode.CreateThreadError:
                    throw new CreateThreadException();
                case EverythingHelper.StateCode.CreateWindowError:
                    throw new CreateWindowException();
                case EverythingHelper.StateCode.InvalidCallError:
                    throw new InvalidCallException();
                case EverythingHelper.StateCode.InvalidIndexError:
                    throw new InvalidIndexException();
                case EverythingHelper.StateCode.IPCError:
                    throw new IPCErrorException();
                case EverythingHelper.StateCode.MemoryError:
                    throw new MemoryErrorException();
                case EverythingHelper.StateCode.RegisterClassExError:
                    throw new RegisterClassExException();
                case EverythingHelper.StateCode.OK:
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
