#nullable enable
using Flow.Launcher.Plugin.BrowserBookmarks.Models;
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.BrowserBookmarks.Services;
using System.ComponentModel;
using System.Linq;
using Flow.Launcher.Plugin.SharedCommands;
using System.Collections.Specialized;
using Flow.Launcher.Plugin.SharedModels;
using System.IO;

namespace Flow.Launcher.Plugin.BrowserBookmarks;

public class Main : ISettingProvider, IPlugin, IAsyncReloadable, IPluginI18n, IContextMenu, IDisposable
{
    internal static PluginInitContext Context { get; set; } = null!;
    private static Settings _settings = null!;
    
    private BookmarkLoaderService _bookmarkLoader = null!;
    private FaviconService _faviconService = null!;
    private BookmarkWatcherService _bookmarkWatcher = null!;

    private List<Bookmark> _bookmarks = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

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
        var bookmarks = _bookmarks; // use a local copy

        if (!string.IsNullOrEmpty(search))
        {
            return bookmarks
                .Select(b =>
                {
                    var match = Context.API.FuzzySearch(search, b.Name);
                    if(!match.IsSearchPrecisionScoreMet())
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
        var bookmarks = await _bookmarkLoader.LoadBookmarksAsync(_cancellationTokenSource.Token);
        
        // Atomically swap the list. This prevents the Query method from seeing a partially loaded list.
        Volatile.Write(ref _bookmarks, bookmarks);

        _bookmarkWatcher.UpdateWatchers(_bookmarkLoader.DiscoveredBookmarkFiles);
        
        // Fire and forget favicon processing to not block the UI
        _ = _faviconService.ProcessBookmarkFavicons(_bookmarks, _cancellationTokenSource.Token);
    }
    
    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = ReloadDataAsync();
    }

    private void OnCustomBrowsersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = ReloadDataAsync();
    }
    
    private void OnBookmarkFileChanged()
    {
        _ = ReloadDataAsync();
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
                    catch(Exception ex)
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
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _faviconService.Dispose();
        _bookmarkWatcher.Dispose();
    }
}
