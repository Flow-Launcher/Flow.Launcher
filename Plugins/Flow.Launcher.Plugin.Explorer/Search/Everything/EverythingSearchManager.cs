using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Explorer.Exceptions;
using Flow.Launcher.Plugin.Explorer.Search.IProvider;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    public class EverythingSearchManager : IIndexProvider, IContentIndexProvider, IPathIndexProvider
    {
        private static readonly string ClassName = nameof(EverythingSearchManager);

        private Settings Settings { get; }

        public EverythingSearchManager(Settings settings)
        {
            Settings = settings;
        }

        private async ValueTask ThrowIfEverythingNotAvailableAsync(CancellationToken token = default)
        {
            try
            {
                if (!await EverythingApi.IsEverythingRunningAsync(token))
                    throw new EngineNotAvailableException(
                        Enum.GetName(Settings.IndexSearchEngineOption.Everything)!,
                        Localize.flowlauncher_plugin_everything_click_to_launch_or_install(),
                        Localize.flowlauncher_plugin_everything_is_not_running(),
                        Constants.EverythingErrorImagePath,
                        ClickToInstallEverythingAsync);
            }
            catch (DllNotFoundException)
            {
                throw new EngineNotAvailableException(
                    Enum.GetName(Settings.IndexSearchEngineOption.Everything)!,
                    "Please check whether your system is x86 or x64",
                    Constants.GeneralSearchErrorImagePath,
                    Localize.flowlauncher_plugin_everything_sdk_issue());
            }
        }

        private async ValueTask<bool> ClickToInstallEverythingAsync(ActionContext _)
        {
            try
            {
                var installedPath = await EverythingDownloadHelper.PromptDownloadIfNotInstallAsync(Settings.EverythingInstalledPath, Main.Context.API);

                if (installedPath == null)
                {
                    Main.Context.API.ShowMsgError(Localize.flowlauncher_plugin_everything_not_found());
                    Main.Context.API.LogError(ClassName, "Unable to find Everything.exe");

                    return false;
                }

                Settings.EverythingInstalledPath = installedPath;
                Process.Start(installedPath, "-startup");

                return true;
            }
            // Sometimes Everything installation will fail because of permission issues or file not found issues
            // Just let the user know that Everything is not installed properly and ask them to install it manually
            catch (Exception e)
            {
                Main.Context.API.ShowMsgError(Localize.flowlauncher_plugin_everything_install_issue());
                Main.Context.API.LogException(ClassName, "Failed to install Everything", e);

                return false;
            }
        }

        public async IAsyncEnumerable<SearchResult> SearchAsync(string search, [EnumeratorCancellation] CancellationToken token, IEnumerable<ResultType> allowedResultTypes = null)
        {
            await ThrowIfEverythingNotAvailableAsync(token);

            if (token.IsCancellationRequested)
                yield break;

            var searchKeyword = BuildSearchKeyword(search, allowedResultTypes);

            var option = new EverythingSearchOption(searchKeyword, 
                Settings.SortOption, 
                MaxCount: Settings.MaxResult, 
                IsFullPathSearch: Settings.EverythingSearchFullPath, 
                IsRunCounterEnabled: Settings.EverythingEnableRunCount);

            await foreach (var result in EverythingApi.SearchAsync(option, token))
                yield return result;
        }

        private string BuildSearchKeyword(string search, IEnumerable<ResultType> allowedResultTypes)
        {
            var filters = new List<string>();

            var typeFilter = BuildTypeFilter(allowedResultTypes);
            if (!string.IsNullOrEmpty(typeFilter))
                filters.Add(typeFilter);

            var extensionFilter = BuildExtensionExclusionFilter();
            if (!string.IsNullOrEmpty(extensionFilter))
                filters.Add(extensionFilter);

            if (filters.Count == 0)
                return search;

            var combinedFilters = string.Join(" ", filters);
            return string.IsNullOrEmpty(search) ? combinedFilters : $"{combinedFilters} {search}";
        }

        private static string BuildTypeFilter(IEnumerable<ResultType> allowedResultTypes)
        {
            if (allowedResultTypes == null)
                return "";

            var hasFile = allowedResultTypes.Contains(ResultType.File);
            var hasFolder = allowedResultTypes.Contains(ResultType.Folder);
            var hasVolume = allowedResultTypes.Contains(ResultType.Volume);

            return (hasFile, hasFolder, hasVolume) switch
            {
                (true, false, false) => "file:",
                (false, true, false) => "folder:",
                (false, false, true) => "volume:",
                (true, true, false) => "<file:|folder:>",
                (true, false, true) => "<file:|volume:>",
                (false, true, true) => "<folder:|volume:>",
                _ => "" // No filtering needed when all allowed or unspecified
            };
        }

        private string BuildExtensionExclusionFilter()
        {
            // Split extensions, remove whitespace, and add dot prefix
            var extensions = Settings.ExcludedFileTypeList
                .Where(ext => !string.IsNullOrWhiteSpace(ext))
                .Select(ext => $"!*.{ext}")
                .ToArray();

            if (extensions.Length == 0)
                return "";

            // Everything syntax: !*.ext1 !*.ext2 to exclude these extensions
            return string.Join(" ", extensions);
        }

        public async IAsyncEnumerable<SearchResult> ContentSearchAsync(string plainSearch, string contentSearch,
            [EnumeratorCancellation] CancellationToken token)
        {
            await ThrowIfEverythingNotAvailableAsync(token);

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

            // Apply excluded file types in content search
            var searchKeyword = BuildSearchKeyword(plainSearch, new[] { ResultType.File });

            var option = new EverythingSearchOption(searchKeyword,
                Settings.SortOption,
                IsContentSearch: true,
                ContentSearchKeyword: contentSearch,
                MaxCount: Settings.MaxResult,
                IsFullPathSearch: Settings.EverythingSearchFullPath,
                IsRunCounterEnabled: Settings.EverythingEnableRunCount);

            await foreach (var result in EverythingApi.SearchAsync(option, token))
            {
                yield return result;
            }
        }

        public async IAsyncEnumerable<SearchResult> EnumerateAsync(string path, string search, bool recursive, [EnumeratorCancellation] CancellationToken token)
        {
            await ThrowIfEverythingNotAvailableAsync(token);

            if (token.IsCancellationRequested)
                yield break;

            // Apply excluded file types in path enumeration
            var searchKeyword = BuildSearchKeyword(search, null);

            var option = new EverythingSearchOption(searchKeyword,
                Settings.SortOption,
                ParentPath: path,
                IsRecursive: recursive,
                MaxCount: Settings.MaxResult,
                IsFullPathSearch: Settings.EverythingSearchFullPath,
                IsRunCounterEnabled: Settings.EverythingEnableRunCount);

            await foreach (var result in EverythingApi.SearchAsync(option, token))
                yield return result;
        }
    }
}
