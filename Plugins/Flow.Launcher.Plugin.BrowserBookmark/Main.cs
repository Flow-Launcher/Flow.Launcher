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

public class Main : ISettingProvider, IPlugin, IReloadable, IPluginI18n, IContextMenu, IDisposable
{
    private static readonly string ClassName = nameof(Main);

    private static Main _instance;

    internal static string _faviconCacheDir;

    internal static PluginInitContext _context;

    internal static Settings _settings;

    private static List<Bookmark> _cachedBookmarks = new();

    private volatile bool _isInitialized = false;

    // A flag to prevent queuing multiple reloads.
    private static volatile bool _isReloading = false;

    // Last time a periodic check triggered a Firefox reload.
    private static DateTime _firefoxLastReload;

    private static readonly SemaphoreSlim _initializationSemaphore = new(1, 1);

    private static readonly object _periodicReloadLock = new();

    private const string DefaultIconPath = @"Images\bookmark.png";

    private static CancellationTokenSource _debounceTokenSource;

    public void Init(PluginInitContext context)
    {
        _instance = this;
        _context = context;
        _settings = context.API.LoadSettingJsonStorage<Settings>();
        _firefoxLastReload = DateTime.UtcNow;

        _faviconCacheDir = Path.Combine(
            context.CurrentPluginMetadata.PluginCacheDirectoryPath,
            "FaviconCache");

        // Start loading bookmarks asynchronously without blocking Init.
        _ = LoadBookmarksInBackgroundAsync();
    }

    private async Task LoadBookmarksInBackgroundAsync()
    {
        // Prevent concurrent loading operations.
        await _initializationSemaphore.WaitAsync();
        try
        {
            // Set initializing state inside the lock. This ensures Query() will show
            // the "initializing" message during the entire reload process.
            _isInitialized = false;

            // Clear data stores inside the lock to ensure a clean slate for the reload.
            _cachedBookmarks.Clear();
            try
            {
                if (Directory.Exists(_faviconCacheDir))
                {
                    Directory.Delete(_faviconCacheDir, true);
                }
            }
            catch (Exception e)
            {
                _context.API.LogException(ClassName, $"Failed to clear favicon cache folder: {_faviconCacheDir}", e);
            }

            // The loading operation itself is wrapped in a try/catch to ensure
            // that even if it fails, the plugin returns to a stable, initialized state.
            try
            {
                if (!_context.CurrentPluginMetadata.Disabled)
                {
                    // Validate the cache directory before loading all bookmarks because Flow needs this directory to storage favicons
                    FilesFolders.ValidateDirectory(_faviconCacheDir);
                    _cachedBookmarks = await Task.Run(() => BookmarkLoader.LoadAllBookmarks(_settings));

                    // Pre-validate all icon paths once to avoid doing it on every query.
                    foreach (var bookmark in _cachedBookmarks)
                    {
                        if (string.IsNullOrEmpty(bookmark.FaviconPath) || !File.Exists(bookmark.FaviconPath))
                        {
                            bookmark.FaviconPath = DefaultIconPath;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _context.API.LogException(ClassName, "An error occurred while trying to load bookmarks.", e);
            }
            finally
            {
                // CRITICAL: Always mark the plugin as initialized, even on failure.
                // This prevents the plugin from getting stuck in the "initializing" state.
                _isInitialized = true;
            }
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    public List<Result> Query(Query query)
    {
        // Smart check for Firefox: periodically trigger a background reload on query.
        // This avoids watching the "hot" places.sqlite file but keeps data reasonably fresh.
        if (!_isReloading && DateTime.UtcNow - _firefoxLastReload > TimeSpan.FromMinutes(2))
        {
            lock (_periodicReloadLock)
            {
                if (!_isReloading && DateTime.UtcNow - _firefoxLastReload > TimeSpan.FromMinutes(2))
                {
                    _isReloading = true;
                    _context.API.LogInfo(ClassName, "Periodic check triggered a background reload of bookmarks.");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ReloadAllBookmarksAsync(false);
                            _firefoxLastReload = DateTime.UtcNow;
                        }
                        catch (Exception e)
                        {
                            _context.API.LogException(ClassName, "Periodic reload failed", e);
                        }
                        finally
                        {
                            _isReloading = false;
                        }
                    });
                }
            }
        }

        // Immediately return if the initial load is not complete, providing feedback to the user.
        if (!_isInitialized)
        {
            var initializingTitle = _context.API.GetTranslation("flowlauncher_plugin_browserbookmark_plugin_name");
            var initializingSubTitle = _context.API.GetTranslation("flowlauncher_plugin_browserbookmark_plugin_initializing");

            return new List<Result>
            {
                new()
                {
                    Title = initializingTitle,
                    SubTitle = initializingSubTitle,
                    IcoPath = DefaultIconPath
                }
            };
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

    // The watcher now monitors the directory but intelligently filters events.
    internal static void RegisterBrowserDataDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        if (Watchers.Any(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var watcher = new FileSystemWatcher(path)
        {
            // Watch the directory, not a specific file, to catch WAL journal updates.
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
        // Event filter: only react to changes in key database files or their journals.
        var file = e.Name.AsSpan();
        if (!(file.StartsWith("Bookmarks") || file.StartsWith("Favicons")))
        {
            return; // Ignore irrelevant file changes.
        }

        var oldCts = Interlocked.Exchange(ref _debounceTokenSource, new CancellationTokenSource());
        oldCts?.Cancel();
        oldCts?.Dispose();

        var newCts = _debounceTokenSource;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), newCts.Token);
                _context.API.LogInfo(ClassName, $"Bookmark file change detected ({e.Name}). Reloading bookmarks after delay.");
                await ReloadAllBookmarksAsync(false);
            }
            catch (TaskCanceledException)
            {
                // Debouncing in action
            }
            catch (Exception ex)
            {
                _context.API.LogException(ClassName, $"Debounced reload failed for {e.Name}", ex);
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

            // Simply dispose watchers if needed and then call the main loading method.
            // All state management is now handled inside LoadBookmarksInBackgroundAsync.
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
