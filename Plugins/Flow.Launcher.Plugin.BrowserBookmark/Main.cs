using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin.BrowserBookmark.Commands;
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using Flow.Launcher.Plugin.BrowserBookmark.Views;
using Flow.Launcher.Plugin.SharedCommands;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading;

namespace Flow.Launcher.Plugin.BrowserBookmark
{
    public class Main : ISettingProvider, IPlugin, IReloadable, IPluginI18n, IContextMenu, IDisposable
    {
        private static PluginInitContext context;

        private static List<Bookmark> cachedBookmarks = new List<Bookmark>();

        private static Settings _settings;

        private static bool initialized = false;

        public void Init(PluginInitContext context)
        {
            Main.context = context;

            _settings = context.API.LoadSettingJsonStorage<Settings>();

            LoadBookmarksIfEnabled();

            initialized = true;
        }

        private static void LoadBookmarksIfEnabled()
        {
            if (context.CurrentPluginMetadata.Disabled)
            {
                // Don't load or monitor files if disabled
                // Note: It doesn't start loading or monitoring if enabled later, you need to manually reload data
                return;
            }

            cachedBookmarks = BookmarkLoader.LoadAllBookmarks(_settings);
            _ = MonitorRefreshQueueAsync();
        }

        public List<Result> Query(Query query)
        {
            if (!initialized)
            {
                LoadBookmarksIfEnabled();
                initialized = true;
            }

            string param = query.Search.TrimStart();

            // Should top results be returned? (true if no search parameters have been passed)
            var topResults = string.IsNullOrEmpty(param);


            if (!topResults)
            {
                // Since we mixed chrome and firefox bookmarks, we should order them again                
                var returnList = cachedBookmarks.Select(c => new Result()
                {
                    Title = c.Name,
                    SubTitle = c.Url,
                    IcoPath = @"Images\bookmark.png",
                    Score = BookmarkLoader.MatchProgram(c, param).Score,
                    Action = _ =>
                    {
                        context.API.OpenUrl(c.Url);

                        return true;
                    },
                    ContextData = new BookmarkAttributes
                    {
                        Url = c.Url
                    }
                }).Where(r => r.Score > 0);
                return returnList.ToList();
            }
            else
            {
                return cachedBookmarks.Select(c => new Result()
                {
                    Title = c.Name,
                    SubTitle = c.Url,
                    IcoPath = @"Images\bookmark.png",
                    Score = 5,
                    Action = _ =>
                    {
                        context.API.OpenUrl(c.Url);
                        return true;
                    },
                    ContextData = new BookmarkAttributes
                    {
                        Url = c.Url
                    }
                }).ToList();
            }
        }


        private static Channel<byte> refreshQueue = Channel.CreateBounded<byte>(1);

        private static SemaphoreSlim fileMonitorSemaphore = new(1, 1);

        private static async Task MonitorRefreshQueueAsync()
        {
            if (fileMonitorSemaphore.CurrentCount < 1)
            {
                return;
            }
            await fileMonitorSemaphore.WaitAsync();
            var reader = refreshQueue.Reader;
            while (await reader.WaitToReadAsync())
            {
                await Task.Delay(5000);
                if (reader.TryRead(out _))
                {
                    ReloadAllBookmarks();
                }
            }
            fileMonitorSemaphore.Release();
        }

        private static readonly List<FileSystemWatcher> Watchers = new();

        internal static void RegisterBookmarkFile(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory) || !File.Exists(path))
            {
                return;
            }
            if (context.CurrentPluginMetadata.Disabled)
            {
                return;
            }
            if (Watchers.Any(x => x.Path.Equals(directory, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
            
            var watcher = new FileSystemWatcher(directory!);
            watcher.Filter = Path.GetFileName(path);

            watcher.NotifyFilter = NotifyFilters.FileName |
                                   NotifyFilters.LastWrite |
                                   NotifyFilters.Size;

            watcher.Changed += static (_, _) =>
            {
                refreshQueue.Writer.TryWrite(default);
            };

            watcher.Renamed += static (_, _) =>
            {
                refreshQueue.Writer.TryWrite(default);
            };

            watcher.EnableRaisingEvents = true;
            
            Watchers.Add(watcher);
        }

        public void ReloadData()
        {
            ReloadAllBookmarks();
        }

        public static void ReloadAllBookmarks()
        {
            cachedBookmarks.Clear();
            DisposeFileWatchers();
            LoadBookmarksIfEnabled();
        }

        public string GetTranslatedPluginTitle()
        {
            return context.API.GetTranslation("flowlauncher_plugin_browserbookmark_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return context.API.GetTranslation("flowlauncher_plugin_browserbookmark_plugin_description");
        }

        public Control CreateSettingPanel()
        {
            return new SettingsControl(_settings);
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            return new List<Result>()
            {
                new Result
                {
                    Title = context.API.GetTranslation("flowlauncher_plugin_browserbookmark_copyurl_title"),
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_browserbookmark_copyurl_subtitle"),
                    Action = _ =>
                    {
                        try
                        {
                            Clipboard.SetDataObject(((BookmarkAttributes)selectedResult.ContextData).Url);

                            return true;
                        }
                        catch (Exception e)
                        {
                            var message = "Failed to set url in clipboard";
                            Log.Exception("Main", message, e, "LoadContextMenus");

                            context.API.ShowMsg(message);

                            return false;
                        }
                    },
                    IcoPath = "Images\\copylink.png",
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
}
