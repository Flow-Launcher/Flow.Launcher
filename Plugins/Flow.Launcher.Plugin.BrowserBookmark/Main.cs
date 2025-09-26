#nullable enable
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.BrowserBookmark.Services;
using System.ComponentModel;
using System.Linq;
using System.Collections.Specialized;
using System.IO;

namespace Flow.Launcher.Plugin.BrowserBookmark;

public class Main : ISettingProvider, IPlugin, IAsyncReloadable, IPluginI18n, IContextMenu, IDisposable
{
    internal static PluginInitContext Context { get; set; } = null!;
    private static Settings _settings = null!;

    private BookmarkLoaderService _bookmarkLoader = null!;
    private FaviconService _faviconService = null!;
    private BookmarkWatcherService _bookmarkWatcher = null!;

    private List<Bookmark> _bookmarks = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private PeriodicTimer? _firefoxBookmarkTimer;
    private static readonly TimeSpan FirefoxPollingInterval = TimeSpan.FromHours(3);
    private readonly SemaphoreSlim _reloadGate = new(1, 1);

    public void Init(PluginInitContext context)
    {
        Context = context;
        _settings = context.API.LoadSettingJsonStorage<Settings>();
        _settings.PropertyChanged += OnSettingsPropertyChanged;
        _settings.CustomBrowsers.CollectionChanged += OnCustomBrowsersChanged;

        var tempPath = SetupTempDirectory();

        _bookmarkLoader = new BookmarkLoaderService(Context, _settings, tempPath);
        _faviconService = new FaviconService(Context, _settings, tempPath);
        _bookmarkWatcher = new BookmarkWatcherService();
        _bookmarkWatcher.OnBookmarkFileChanged += OnBookmarkFileChanged;

        // Fire and forget the initial load to make Flow's UI responsive immediately.
        _ = ReloadDataAsync();
        StartFirefoxBookmarkTimer();
    }

    private string SetupTempDirectory()
    {
        var tempPath = Path.Combine(Context.CurrentPluginMetadata.PluginCacheDirectoryPath, "Temp");
        try
        {
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);
            }
            Directory.CreateDirectory(tempPath);
        }
        catch (Exception e)
        {
            Context.API.LogException(nameof(Main), "Failed to set up temporary directory.", e);
        }
        return tempPath;
    }

    public List<Result> Query(Query query)
    {
        var search = query.Search.Trim();
        var bookmarks = Volatile.Read(ref _bookmarks); // use a local copy with proper memory barrier

        if (!string.IsNullOrEmpty(search))
        {
            return bookmarks
                .Select(b =>
                {
                    var match = Context.API.FuzzySearch(search, b.Name);
                    if (!match.IsSearchPrecisionScoreMet())
                        match = Context.API.FuzzySearch(search, b.Url);
                    return (b, match);
                })
                .Where(t => t.match.IsSearchPrecisionScoreMet())
                .OrderByDescending(t => t.match.Score)
                .Select(t => CreateResult(t.b, t.match.Score))
                .ToList();
        }

        return bookmarks.Select(b => CreateResult(b, 0)).ToList();
    }

    private Result CreateResult(Bookmark bookmark, int score) => new()
    {
        Title = bookmark.Name,
        SubTitle = bookmark.Url,
        IcoPath = !string.IsNullOrEmpty(bookmark.FaviconPath)
            ? bookmark.FaviconPath
            : @"Images\bookmark.png",
        Score = score,
        Action = _ =>
        {
            Context.API.OpenUrl(bookmark.Url);
            return true;
        },
        ContextData = bookmark.Url
    };

    public async Task ReloadDataAsync()
    {
        await _reloadGate.WaitAsync(_cancellationTokenSource.Token);
        try
        {
            var (bookmarks, discoveredFiles) = await _bookmarkLoader.LoadBookmarksAsync(_cancellationTokenSource.Token);

            // Atomically swap the list. This prevents the Query method from seeing a partially loaded list.
            Volatile.Write(ref _bookmarks, bookmarks);

            _bookmarkWatcher.UpdateWatchers(discoveredFiles);

            // Fire and forget favicon processing to not block the UI
            _ = _faviconService.ProcessBookmarkFavicons(_bookmarks, _cancellationTokenSource.Token);
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Settings.LoadFirefoxBookmark))
        {
            StartFirefoxBookmarkTimer();
        }
        _ = ReloadDataAsync();
    }

    private void OnCustomBrowsersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        StartFirefoxBookmarkTimer();
        _ = ReloadDataAsync();
    }

    private void OnBookmarkFileChanged()
    {
        _ = ReloadDataAsync();
    }

    private void StartFirefoxBookmarkTimer()
    {
        _firefoxBookmarkTimer?.Dispose();

        if (!_settings.LoadFirefoxBookmark && !_settings.CustomBrowsers.Any(x => x.BrowserType == BrowserType.Firefox))
            return;

        _firefoxBookmarkTimer = new PeriodicTimer(FirefoxPollingInterval);

        var timer = _firefoxBookmarkTimer!;
        _ = Task.Run(async () =>
        {
            try
            {
                while (await timer.WaitForNextTickAsync(_cancellationTokenSource.Token))
                {
                    await ReloadFirefoxBookmarksAsync();
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        }, _cancellationTokenSource.Token);
    }

    private async Task ReloadFirefoxBookmarksAsync()
    {
        // Share the same gate to avoid conflicting with full reloads
        await _reloadGate.WaitAsync(_cancellationTokenSource.Token);
        try
        {
            Context.API.LogInfo(nameof(Main), "Starting periodic reload of Firefox bookmarks.");

            var firefoxLoaders = _bookmarkLoader.GetFirefoxBookmarkLoaders().ToList();
            if (!firefoxLoaders.Any())
            {
                Context.API.LogInfo(nameof(Main), "No Firefox bookmark loaders enabled, skipping reload.");
                return;
            }

            var tasks = firefoxLoaders.Select(async loader =>
            {
                var loadedBookmarks = new List<Bookmark>();
                try
                {
                    await foreach (var bookmark in loader.GetBookmarksAsync(_cancellationTokenSource.Token))
                    {
                        loadedBookmarks.Add(bookmark);
                    }
                    return (Loader: loader, Bookmarks: loadedBookmarks, Success: true);
                }
                catch (OperationCanceledException)
                {
                    return (Loader: loader, Bookmarks: new List<Bookmark>(), Success: false);
                }
                catch (Exception e)
                {
                    Context.API.LogException(nameof(Main), $"Failed to load bookmarks from {loader.Name}.", e);
                    return (Loader: loader, Bookmarks: new List<Bookmark>(), Success: false);
                }
            });

            var results = await Task.WhenAll(tasks);
            var successfulResults = results.Where(r => r.Success).ToList();

            if (!successfulResults.Any())
            {
                Context.API.LogInfo(nameof(Main), "No Firefox bookmarks successfully reloaded.");
                return;
            }

            var newFirefoxBookmarks = successfulResults.SelectMany(r => r.Bookmarks).ToList();
            var successfulLoaderNames = successfulResults.Select(r => r.Loader.Name).ToHashSet();

            var currentBookmarks = Volatile.Read(ref _bookmarks);

            var otherBookmarks = currentBookmarks.Where(b => !successfulLoaderNames.Any(name => b.Source.StartsWith(name, StringComparison.OrdinalIgnoreCase)));

            var newBookmarkList = otherBookmarks.Concat(newFirefoxBookmarks).Distinct().ToList();

            Volatile.Write(ref _bookmarks, newBookmarkList);

            Context.API.LogInfo(nameof(Main), $"Periodic reload complete. Loaded {newFirefoxBookmarks.Count} Firefox bookmarks from {successfulLoaderNames.Count} sources.");

            _ = _faviconService.ProcessBookmarkFavicons(newFirefoxBookmarks, _cancellationTokenSource.Token);
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    public Control CreateSettingPanel()
    {
        return new Views.SettingsControl(_settings);
    }

    public string GetTranslatedPluginTitle()
    {
        return Localize.flowlauncher_plugin_browserbookmark_plugin_name();
    }

    public string GetTranslatedPluginDescription()
    {
        return Localize.flowlauncher_plugin_browserbookmark_plugin_description();
    }

    public List<Result> LoadContextMenus(Result selectedResult)
    {
        if (selectedResult.ContextData is not string url)
            return new List<Result>();

        return new List<Result>
        {
            new()
            {
                Title = Localize.flowlauncher_plugin_browserbookmark_copyurl_title(),
                SubTitle = Localize.flowlauncher_plugin_browserbookmark_copyurl_subtitle(),
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue8c8"),
                IcoPath = @"Images\copylink.png",
                Action = _ =>
                {
                    try
                    {
                        Context.API.CopyToClipboard(url);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Context.API.LogException(nameof(Main), "Failed to copy URL to clipboard", ex);
                        Context.API.ShowMsgError(Localize.flowlauncher_plugin_browserbookmark_copy_failed());
                        return false;
                    }
                }
            }
        };
    }

    public void Dispose()
    {
        _settings.PropertyChanged -= OnSettingsPropertyChanged;
        _settings.CustomBrowsers.CollectionChanged -= OnCustomBrowsersChanged;
        _firefoxBookmarkTimer?.Dispose();
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _faviconService.Dispose();
        _bookmarkWatcher.Dispose();
    }
}
