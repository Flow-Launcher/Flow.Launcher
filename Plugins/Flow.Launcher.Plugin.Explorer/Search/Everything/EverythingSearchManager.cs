using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Explorer.Exceptions;
using Flow.Launcher.Plugin.Explorer.Search.IProvider;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    public class EverythingSearchManager : IIndexProvider, IContentIndexProvider, IPathIndexProvider
    {
        private Settings Settings { get; }
        private readonly EverythingAvailabilityService _availabilityService;
        private readonly Lock _syncRoot = new();
        private bool isApiInitialized;
        private IEverythingApi api;

        public EverythingSearchManager(Settings settings)
        {
            Settings = settings;
            _availabilityService = new EverythingAvailabilityService(settings);
            api = EverythingApiFactory.Create(settings);
        }

        public async IAsyncEnumerable<SearchResult> SearchAsync(string search, [EnumeratorCancellation] CancellationToken token)
        {
            await _availabilityService.EnsureAvailableAsync(api, token);

            if (token.IsCancellationRequested)
                yield break;

            var option = new EverythingSearchOption(search, 
                Settings.SortOption, 
                MaxCount: Settings.MaxResult, 
                IsFullPathSearch: Settings.EverythingSearchFullPath, 
                IsRunCounterEnabled: Settings.EverythingEnableRunCount);

            await foreach (var result in api.SearchAsync(option, token))
                yield return result;
        }

        public async IAsyncEnumerable<SearchResult> ContentSearchAsync(string plainSearch, string contentSearch,
            [EnumeratorCancellation] CancellationToken token)
        {
            await _availabilityService.EnsureAvailableAsync(api, token);

            if (!Settings.EnableEverythingContentSearch)
            {
                throw new EngineNotAvailableException(Enum.GetName(Settings.IndexSearchEngineOption.Everything)!,
                    Localize.flowlauncher_plugin_everything_enable_content_search(),
                    Localize.flowlauncher_plugin_everything_enable_content_search_tips(),
                    Constants.EverythingErrorImagePath,
                    _ =>
                    {
                        Settings.EnableEverythingContentSearch = true;

                        return ValueTask.FromResult(true);
                    });
            }

            if (token.IsCancellationRequested)
                yield break;

            var option = new EverythingSearchOption(plainSearch,
                Settings.SortOption,
                IsContentSearch: true,
                ContentSearchKeyword: contentSearch,
                MaxCount: Settings.MaxResult,
                IsFullPathSearch: Settings.EverythingSearchFullPath,
                IsRunCounterEnabled: Settings.EverythingEnableRunCount);

            await foreach (var result in api.SearchAsync(option, token))
            {
                yield return result;
            }
        }

        public async IAsyncEnumerable<SearchResult> EnumerateAsync(string path, string search, bool recursive, [EnumeratorCancellation] CancellationToken token)
        {
            await _availabilityService.EnsureAvailableAsync(api, token);

            if (token.IsCancellationRequested)
                yield break;

            var option = new EverythingSearchOption(search,
                Settings.SortOption,
                ParentPath: path,
                IsRecursive: recursive,
                MaxCount: Settings.MaxResult,
                IsFullPathSearch: Settings.EverythingSearchFullPath,
                IsRunCounterEnabled: Settings.EverythingEnableRunCount);

            await foreach (var result in api.SearchAsync(option, token))
                yield return result;
        }

        public void InitializeApi(string sdkDirectory)
        {
            lock (_syncRoot)
            {
                if (isApiInitialized)
                    return;

                EverythingSdkLoader.EnsureLoaded(sdkDirectory, Settings.EnableEverything15Support);
                api = EverythingApiFactory.Create(Settings);
                isApiInitialized = true;
            }
        }

        public bool IsFastSortOption(EverythingSortOption sortOption) => api.IsFastSortOption(sortOption);

        public Task IncrementRunCounterAsync(string fileOrFolder) => api.IncrementRunCounterAsync(fileOrFolder);
    }
}
