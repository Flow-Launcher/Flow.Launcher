﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Controls;
using Flow.Launcher.Plugin.BrowserBookmark.Commands;
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using Flow.Launcher.Plugin.BrowserBookmark.Views;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Plugin.BrowserBookmark;

public class Main : ISettingProvider, IPlugin, IReloadable, IPluginI18n, IContextMenu, IDisposable
{
    private static readonly string ClassName = nameof(Main);

    internal static string _faviconCacheDir;

    internal static PluginInitContext _context;

    internal static Settings _settings;

    private static List<Bookmark> _cachedBookmarks = new();

    private static bool _initialized = false;
    
    public void Init(PluginInitContext context)
    {
        _context = context;

        _settings = context.API.LoadSettingJsonStorage<Settings>();

        _faviconCacheDir = Path.Combine(
            context.CurrentPluginMetadata.PluginCacheDirectoryPath,
            "FaviconCache");

        LoadBookmarksIfEnabled();
    }

    private static void LoadBookmarksIfEnabled()
    {
        if (_context.CurrentPluginMetadata.Disabled)
        {
            // Don't load or monitor files if disabled
            return;
        }

        // Validate the cache directory before loading all bookmarks because Flow needs this directory to storage favicons
        FilesFolders.ValidateDirectory(_faviconCacheDir);

        _cachedBookmarks = BookmarkLoader.LoadAllBookmarks(_settings);
        _ = MonitorRefreshQueueAsync();
        _initialized = true;
    }

    public List<Result> Query(Query query)
    {
        // For when the plugin being previously disabled and is now re-enabled
        if (!_initialized)
        {
            LoadBookmarksIfEnabled();
        }

        string param = query.Search.TrimStart();

        // Should top results be returned? (true if no search parameters have been passed)
        var topResults = string.IsNullOrEmpty(param);

        if (!topResults)
        {
            // Since we mixed chrome and firefox bookmarks, we should order them again
            return _cachedBookmarks
                .Select(
                    c => new Result
                    {
                        Title = c.Name,
                        SubTitle = c.Url,
                        IcoPath = !string.IsNullOrEmpty(c.FaviconPath) && File.Exists(c.FaviconPath)
                            ? c.FaviconPath
                            : @"Images\bookmark.png",
                        Score = BookmarkLoader.MatchProgram(c, param).Score,
                        Action = _ =>
                        {
                            _context.API.OpenUrl(c.Url);

                            return true;
                        },
                        ContextData = new BookmarkAttributes { Url = c.Url }
                    }
                )
                .Where(r => r.Score > 0)
                .ToList();
        }
        else
        {
            return _cachedBookmarks
                .Select(
                    c => new Result
                    {
                        Title = c.Name,
                        SubTitle = c.Url,
                        IcoPath = !string.IsNullOrEmpty(c.FaviconPath) && File.Exists(c.FaviconPath)
                            ? c.FaviconPath
                            : @"Images\bookmark.png",
                        Score = 5,
                        Action = _ =>
                        {
                            _context.API.OpenUrl(c.Url);
                            return true;
                        },
                        ContextData = new BookmarkAttributes { Url = c.Url }
                    }
                )
                .ToList();
        }
    }

    private static readonly Channel<byte> _refreshQueue = Channel.CreateBounded<byte>(1);

    private static readonly SemaphoreSlim _fileMonitorSemaphore = new(1, 1);

    private static async Task MonitorRefreshQueueAsync()
    {
        if (_fileMonitorSemaphore.CurrentCount < 1)
        {
            return;
        }
        await _fileMonitorSemaphore.WaitAsync();
        var reader = _refreshQueue.Reader;
        while (await reader.WaitToReadAsync())
        {
            if (reader.TryRead(out _))
            {
                ReloadAllBookmarks(false);
            }
        }
        _fileMonitorSemaphore.Release();
    }

    private static readonly List<FileSystemWatcher> Watchers = new();

    internal static void RegisterBookmarkFile(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory) || !File.Exists(path))
        {
            return;
        }
        if (Watchers.Any(x => x.Path.Equals(directory, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var watcher = new FileSystemWatcher(directory!)
        {
            Filter = Path.GetFileName(path),
            NotifyFilter = NotifyFilters.FileName |
                                   NotifyFilters.LastWrite |
                                   NotifyFilters.Size
        };

        watcher.Changed += static (_, _) =>
        {
            _refreshQueue.Writer.TryWrite(default);
        };

        watcher.Renamed += static (_, _) =>
        {
            _refreshQueue.Writer.TryWrite(default);
        };

        watcher.EnableRaisingEvents = true;

        Watchers.Add(watcher);
    }

    public void ReloadData()
    {
        ReloadAllBookmarks();
    }

    public static void ReloadAllBookmarks(bool disposeFileWatchers = true)
    {
        _cachedBookmarks.Clear();
        if (disposeFileWatchers)
            DisposeFileWatchers();
        LoadBookmarksIfEnabled();
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
