using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Plugin.BrowserBookmark.Commands;
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using Flow.Launcher.Plugin.BrowserBookmark.Views;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Plugin.BrowserBookmark;

public class Main : ISettingProvider, IAsyncPlugin, IReloadable, IPluginI18n, IContextMenu, IDisposable
{
    private static readonly string ClassName = nameof(Main);

    internal static string _faviconCacheDir;

    internal static PluginInitContext _context;

    internal static Settings _settings;

    private static List<Bookmark> _cachedBookmarks = new();

    private static Main _instance;

    private volatile bool _initialized = false;

    private static readonly SemaphoreSlim _initializationSemaphore = new(1, 1);

    private const string DefaultIconPath = @"Images\bookmark.png";

    private static CancellationTokenSource _debounceTokenSource;

    public async Task InitAsync(PluginInitContext context)
    {
        _instance = this;
        _context = context;
        _settings = context.API.LoadSettingJsonStorage<Settings>();

        _faviconCacheDir = Path.Combine(
            context.CurrentPluginMetadata.PluginCacheDirectoryPath,
            "FaviconCache");

        // Start loading bookmarks asynchronously without blocking Init
        _ = LoadBookmarksInBackgroundAsync();
        await Task.CompletedTask;
    }

    private async Task LoadBookmarksInBackgroundAsync()
    {
        if (_context.CurrentPluginMetadata.Disabled)
        {
            // Don't load or monitor files if disabled
            return;
        }

        // Prevent concurrent loading operations.
        await _initializationSemaphore.WaitAsync();
        try
        {
            if (_initialized) return;

            // Validate the cache directory before loading all bookmarks because Flow needs this directory to storage favicons
            FilesFolders.ValidateDirectory(_faviconCacheDir);
            _cachedBookmarks = await Task.Run(() => BookmarkLoader.LoadAllBookmarks(_settings));

            // Pre-validate all icon paths once to avoid doing it on every query
            foreach (var bookmark in _cachedBookmarks)
            {
                if (string.IsNullOrEmpty(bookmark.FaviconPath) || !File.Exists(bookmark.FaviconPath))
                {
                    bookmark.FaviconPath = DefaultIconPath;
                }
            }

            _initialized = true;
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        // For when the plugin being previously disabled and is now re-enabled
        // Or when the plugin is still initializing
        if (!_initialized)
        {
            await LoadBookmarksInBackgroundAsync();
        }

        string param = query.Search.TrimStart();
        bool topResults = string.IsNullOrEmpty(param);

        var results = _cachedBookmarks
            .Select(c =>
            {
                var score = topResults ? 5 : BookmarkLoader.MatchProgram(c, param).Score;
                if (!topResults && score <= 0)
                    return null;

                return new Result
                {
                    Title = c.Name,
                    SubTitle = c.Url,
                    IcoPath = c.FaviconPath, // Use the pre-validated path directly.
                    Score = score,
                    Action = _ =>
                    {
                        _context.API.OpenUrl(c.Url);
                        return true;
                    },
                    ContextData = new BookmarkAttributes { Url = c.Url }
                };
            })
            .Where(r => r != null);

        return (topResults ? results : results.OrderByDescending(r => r.Score)).ToList();
    }

    private static readonly List<FileSystemWatcher> Watchers = new();

    internal static void RegisterBookmarkFile(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory) || !File.Exists(path))
        {
            return;
        }

        if (Watchers.Any(x => x.Path.Equals(directory, StringComparison.OrdinalIgnoreCase) && x.Filter == Path.GetFileName(path)))
        {
            return;
        }

        var watcher = new FileSystemWatcher(directory!)
        {
            Filter = Path.GetFileName(path),
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        watcher.Changed += OnBookmarkFileChanged;
        watcher.Renamed += OnBookmarkFileChanged;
        watcher.Deleted += OnBookmarkFileChanged;
        watcher.Created += OnBookmarkFileChanged;

        Watchers.Add(watcher);
    }

    private static void OnBookmarkFileChanged(object sender, FileSystemEventArgs e)
    {
        var oldCts = Interlocked.Exchange(ref _debounceTokenSource, new CancellationTokenSource());
        oldCts?.Cancel();
        oldCts?.Dispose();

        var newCts = _debounceTokenSource;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), newCts.Token);
                _context.API.LogInfo(ClassName, "Bookmark file change detected. Reloading bookmarks after delay.");
                await ReloadAllBookmarksAsync(false);
            }
            catch (TaskCanceledException)
            {
                // Debouncing in action
            }
        }, newCts.Token);
    }

    public void ReloadData()
    {
        _ = ReloadAllBookmarksAsync();
    }

    public static async Task ReloadAllBookmarksAsync(bool disposeFileWatchers = true)
    {
        try
        {
            if (_instance == null) return;

            _instance._initialized = false;
            _cachedBookmarks.Clear();
            if (disposeFileWatchers)
                DisposeFileWatchers();

            await _instance.LoadBookmarksInBackgroundAsync();
        }
        catch (Exception e)
        {
            _context?.API.LogException(ClassName, "An error occurred while reloading bookmarks", e);
        }
    }

    public string GetTranslatedPluginTitle()
    {
        return _context.API.GetTranslation("flowlauncher_plugin_browserbookmark_plugin_name");
    }

    public string GetTranslatedPluginDescription()
    {
        return _context.API.GetTranslation("flowlauncher_plugin_browserbookmark_plugin_description");
    }

    public Control CreateSettingPanel()
    {
        return new SettingsControl(_settings);
    }

    public List<Result> LoadContextMenus(Result selectedResult)
    {
        return new List<Result>()
        {
            new()
            {
                Title = _context.API.GetTranslation("flowlauncher_plugin_browserbookmark_copyurl_title"),
                SubTitle = _context.API.GetTranslation("flowlauncher_plugin_browserbookmark_copyurl_subtitle"),
                Action = _ =>
                {
                    try
                    {
                        _context.API.CopyToClipboard(((BookmarkAttributes)selectedResult.ContextData).Url);
                        return true;
                    }
                    catch (Exception e)
                    {
                        var message = "Failed to set url in clipboard";
                        _context.API.LogException(ClassName, message, e);
                        _context.API.ShowMsg(message);
                        return false;
                    }
                },
                IcoPath = @"Images\copylink.png",
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue8c8")
            }
        };
    }

    internal class BookmarkAttributes
    {
        internal string Url { get; set; }
    }

    public void Dispose()
    {
        DisposeFileWatchers();
        var cts = Interlocked.Exchange(ref _debounceTokenSource, null);
        cts?.Cancel();
        cts?.Dispose();
    }

    private static void DisposeFileWatchers()
    {
        foreach (var watcher in Watchers)
        {
            watcher.Dispose();
        }
        Watchers.Clear();
    }
}
