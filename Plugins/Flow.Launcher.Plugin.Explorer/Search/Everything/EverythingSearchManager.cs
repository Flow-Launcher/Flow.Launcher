using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                        Main.Context.API.GetTranslation("flowlauncher_plugin_everything_click_to_launch_or_install"),
                        Main.Context.API.GetTranslation("flowlauncher_plugin_everything_is_not_running"),
                        Constants.EverythingErrorImagePath,
                        ClickToInstallEverythingAsync);
            }
            catch (DllNotFoundException)
            {
                throw new EngineNotAvailableException(
                    Enum.GetName(Settings.IndexSearchEngineOption.Everything)!,
                    "Please check whether your system is x86 or x64",
                    Constants.GeneralSearchErrorImagePath,
                    Main.Context.API.GetTranslation("flowlauncher_plugin_everything_sdk_issue"));
            }
        }
        private async ValueTask<bool> ClickToInstallEverythingAsync(ActionContext _)
        {
            var installedPath = await EverythingDownloadHelper.PromptDownloadIfNotInstallAsync(Settings.EverythingInstalledPath, Main.Context.API);

            if (installedPath == null)
            {
                Main.Context.API.ShowMsgError("Unable to find Everything.exe");

                return false;
            }

            Settings.EverythingInstalledPath = installedPath;
            Process.Start(installedPath, "-startup");

            return true;
        }

        public async IAsyncEnumerable<SearchResult> SearchAsync(string search, [EnumeratorCancellation] CancellationToken token)
        {
            await ThrowIfEverythingNotAvailableAsync(token);

            if (token.IsCancellationRequested)
                yield break;

            var option = new EverythingSearchOption(search, Settings.SortOption, IsFullPathSearch: Settings.EverythingSearchFullPath);

            await foreach (var result in EverythingApi.SearchAsync(option, token))
                yield return result;
        }
        public async IAsyncEnumerable<SearchResult> ContentSearchAsync(string plainSearch,
            string contentSearch,
            [EnumeratorCancellation] CancellationToken token)
        {
            await ThrowIfEverythingNotAvailableAsync(token);

            if (!Settings.EnableEverythingContentSearch)
            {
                throw new EngineNotAvailableException(Enum.GetName(Settings.IndexSearchEngineOption.Everything)!,
                    Main.Context.API.GetTranslation("flowlauncher_plugin_everything_enable_content_search"),
                    Main.Context.API.GetTranslation("flowlauncher_plugin_everything_enable_content_search_tips"),
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
                true,
                contentSearch,
                IsFullPathSearch: Settings.EverythingSearchFullPath);

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
                IsFullPathSearch: Settings.EverythingSearchFullPath);

            await foreach (var result in EverythingApi.SearchAsync(option, token))
            {
                yield return result;
            }
        }
    }
}
