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

namespace Flow.Launcher.Plugin.BrowserBookmark
{
    public class Main : ISettingProvider, IPlugin, IReloadable, IPluginI18n, IContextMenu, IDisposable
    {
        private PluginInitContext context;

        private List<Bookmark> cachedBookmarks = new List<Bookmark>();

        private Settings _settings { get; set; }

        public void Init(PluginInitContext context)
        {
            this.context = context;

            _settings = context.API.LoadSettingJsonStorage<Settings>();

            cachedBookmarks = BookmarkLoader.LoadAllBookmarks(_settings);

            _ = MonitorRefreshQueue();
        }

        public List<Result> Query(Query query)
        {
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

        private async Task MonitorRefreshQueue()
        {
            var reader = refreshQueue.Reader;
            while (await reader.WaitToReadAsync())
            {
                await Task.Delay(2000);
                if (reader.TryRead(out _))
                {
                    ReloadData();
                }
            }
        }

        private static readonly List<FileSystemWatcher> Watchers = new();

        internal static void RegisterBookmarkFile(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
                return;
            var watcher = new FileSystemWatcher(directory!);
            if (File.Exists(path))
            {
                var fileName = Path.GetFileName(path);
                watcher.Filter = fileName;
            }

            watcher.NotifyFilter = NotifyFilters.FileName |
                                   NotifyFilters.LastAccess |
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
            cachedBookmarks.Clear();

            cachedBookmarks = BookmarkLoader.LoadAllBookmarks(_settings);
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
                    IcoPath = "Images\\copylink.png"
                }
            };
        }

        internal class BookmarkAttributes
        {
            internal string Url { get; set; }
        }
        public void Dispose()
        {
            foreach (var watcher in Watchers)
            {
                watcher.Dispose();
            }
        }
    }
}