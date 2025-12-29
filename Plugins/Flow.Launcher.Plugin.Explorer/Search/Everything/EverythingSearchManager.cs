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

            var searchKeyword = BuildSearchKeywordWithTypeFilter(search, allowedResultTypes);

            var option = new EverythingSearchOption(searchKeyword, 
                Settings.SortOption, 
                MaxCount: Settings.MaxResult, 
                IsFullPathSearch: Settings.EverythingSearchFullPath, 
                IsRunCounterEnabled: Settings.EverythingEnableRunCount);

            await foreach (var result in EverythingApi.SearchAsync(option, token))
                yield return result;
        }

        private static string BuildSearchKeywordWithTypeFilter(string search, IEnumerable<ResultType> allowedResultTypes)
        {
            if (allowedResultTypes == null)
                return search;

            var typesList = allowedResultTypes as IList<ResultType> ?? allowedResultTypes.ToList();
            var hasFile = typesList.Contains(ResultType.File);
            var hasFolder = typesList.Contains(ResultType.Folder);
            var hasVolume = typesList.Contains(ResultType.Volume);

            var filter = (hasFile, hasFolder, hasVolume) switch
            {
                (true, false, false) => "file:",
                (false, true, false) => "folder:",
                (false, false, true) => "volume:",
                (true, true, false) => "<file:|folder:>",
                (true, false, true) => "<file:|volume:>",
                (false, true, true) => "<folder:|volume:>",
                _ => null // No filtering needed when all allowed or unspecified
            };

            return filter == null ? search : $"{filter} {search}";
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

            var option = new EverythingSearchOption(plainSearch,
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

            var option = new EverythingSearchOption(search,
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
