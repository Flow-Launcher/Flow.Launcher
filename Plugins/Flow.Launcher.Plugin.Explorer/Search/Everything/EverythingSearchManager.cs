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
        private static readonly string ClassName = nameof(EverythingSearchManager);
        private static readonly SemaphoreSlim _dllSemaphore = new(1, 1);
        private static volatile bool _dllLoadedForSdk3 = true;

        private Settings Settings { get; }
        private readonly Lock _syncRoot = new();
        private IEverythingApi _api;

        public EverythingSearchManager(Settings settings)
        {
            Settings = settings;
            _api = CreateApi(Settings.EnableEverything15Support, GetNormalizedInstanceName(Settings.Everything15InstanceName));
        }

        private async ValueTask ThrowIfEverythingNotAvailableAsync(CancellationToken token = default)
        {
            try
            {
                if (!await _api.IsEverythingRunningAsync(token))
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

        public async IAsyncEnumerable<SearchResult> SearchAsync(string search, [EnumeratorCancellation] CancellationToken token)
        {
            await ThrowIfEverythingNotAvailableAsync(token);

            if (token.IsCancellationRequested)
                yield break;

            var option = new EverythingSearchOption(search, 
                Settings.SortOption, 
                MaxCount: Settings.MaxResult, 
                IsFullPathSearch: Settings.EverythingSearchFullPath, 
                IsRunCounterEnabled: Settings.EverythingEnableRunCount);

            await foreach (var result in _api.SearchAsync(option, token))
                yield return result;
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

            await foreach (var result in _api.SearchAsync(option, token))
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

            await foreach (var result in _api.SearchAsync(option, token))
                yield return result;
        }
        public void ReloadApi(string sdkDirectory)
        {
            lock (_syncRoot)
            {
                LoadDllCore(Settings.EnableEverything15Support, sdkDirectory);
                _api = CreateApi(Settings.EnableEverything15Support, GetNormalizedInstanceName(Settings.Everything15InstanceName));
            }
        }

        public bool IsFastSortOption(EverythingSortOption sortOption) => _api.IsFastSortOption(sortOption);

        public Task IncrementRunCounterAsync(string fileOrFolder) => _api.IncrementRunCounterAsync(fileOrFolder);

        private static void LoadDllCore(bool enableEverything15Support, string sdkDirectory)
        {
            _dllSemaphore.Wait();
            try
            {
                var supportChanged = _dllLoadedForSdk3 != enableEverything15Support;
                _dllLoadedForSdk3 = enableEverything15Support;

                if (enableEverything15Support)
                {
                    if (supportChanged || !Everything3ApiDllImport.IsLoaded)
                    {
                        if (EverythingApiDllImport.IsLoaded)
                            EverythingApiDllImport.Unload();

                        Everything3ApiDllImport.Load(sdkDirectory);
                    }
                }
                else
                {
                    if (supportChanged || !EverythingApiDllImport.IsLoaded)
                    {
                        if (Everything3ApiDllImport.IsLoaded)
                            Everything3ApiDllImport.Unload();

                        EverythingApiDllImport.Load(sdkDirectory);
                    }
                }
            }
            finally
            {
                _dllSemaphore.Release();
            }
        }

        private static string GetNormalizedInstanceName(string instanceName) => string.IsNullOrWhiteSpace(instanceName)
            ? EverythingApiV3.DefaultEverything15InstanceName
            : instanceName.Trim();

        private static IEverythingApi CreateApi(bool enableEverything15Support, string instanceName)
        {
            return enableEverything15Support
                ? new EverythingApiV3(instanceName)
                : new LegacyEverythingApi();
        }
    }
}
