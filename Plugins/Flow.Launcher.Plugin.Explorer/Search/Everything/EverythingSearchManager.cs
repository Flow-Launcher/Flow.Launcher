using System;
using Flow.Launcher.Plugin.Explorer.Search.WindowsIndex;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Explorer.Exceptions;
using Flow.Launcher.Plugin.Explorer.Search.Everything.Exceptions;
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
            if (!await EverythingApi.IsEverythingRunningAsync(token))
                throw new EngineNotAvailableException(
                    Enum.GetName(Settings.IndexSearchEngineOption.Everything),
                    "Click to Open or Download Everything",
                    "Everything is not running.",
                    async _ =>
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
                    })
                {
                    ErrorIcon = Constants.EverythingErrorImagePath
                };
        }

        public async IAsyncEnumerable<SearchResult> SearchAsync(string search, [EnumeratorCancellation] CancellationToken token)
        {
            await ThrowIfEverythingNotAvailableAsync(token);
            if (token.IsCancellationRequested)
                yield break;
            var option = new EverythingSearchOption(search, Settings.SortOption);
            await foreach (var result in EverythingApi.SearchAsync(option, token))
                yield return result;
        }
        public async IAsyncEnumerable<SearchResult> ContentSearchAsync(string plainSearch,
            string contentSearch, [EnumeratorCancellation] CancellationToken token)
        {
            await ThrowIfEverythingNotAvailableAsync(token);
            if (!Settings.EnableEverythingContentSearch)
            {
                throw new EngineNotAvailableException(Enum.GetName(Settings.IndexSearchEngineOption.Everything),
                    "Click to Enable Everything Content Search (only applicable to Everything 1.5+ with indexed content)",
                    "Everything Content Search is not enabled.",
                    _ =>
                    {
                        Settings.EnableEverythingContentSearch = true;
                        return ValueTask.FromResult(true);
                    })
                {
                    ErrorIcon = Constants.EverythingErrorImagePath
                };
            }
            if (token.IsCancellationRequested)
                yield break;

            var option = new EverythingSearchOption(plainSearch,
                Settings.SortOption,
                true,
                contentSearch);

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
                IsRecursive: recursive);

            await foreach (var result in EverythingApi.SearchAsync(option, token))
            {
                yield return result;
            }
        }
    }
}
