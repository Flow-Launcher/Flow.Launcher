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

namespace Flow.Launcher.Plugin.BrowserBookmark
{
    public class Main : ISettingProvider, IPlugin, IReloadable, IPluginI18n, IContextMenu
    {
        internal static PluginInitContext context;

        private List<Bookmark> cachedBookmarks = new List<Bookmark>();

        private Settings _settings { get; set;}

        public void Init(PluginInitContext context)
        {
            Main.context = context;
            
            _settings = context.API.LoadSettingJsonStorage<Settings>();

            cachedBookmarks = Bookmarks.LoadAllBookmarks();
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
                    Score = Bookmarks.MatchProgram(c, param).Score,
                    Action = _ =>
                    {
                        if (_settings.OpenInNewBrowserWindow)
                        {
                            c.Url.NewBrowserWindow(_settings.BrowserPath);
                        }
                        else
                        {
                            c.Url.NewTabInBrowser(_settings.BrowserPath);
                        }

                        return true;
                    },
                    ContextData = new BookmarkAttributes { Url = c.Url }
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
                        if (_settings.OpenInNewBrowserWindow)
                        {
                            c.Url.NewBrowserWindow(_settings.BrowserPath);
                        }
                        else
                        {
                            c.Url.NewTabInBrowser(_settings.BrowserPath);
                        }

                        return true;
                    },
                    ContextData = new BookmarkAttributes { Url = c.Url }
                }).ToList();
            }
        }

        public void ReloadData()
        {
            cachedBookmarks.Clear();

            cachedBookmarks = Bookmarks.LoadAllBookmarks();
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
            return new List<Result>() {
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
                            Log.Exception("Main",message, e, "LoadContextMenus");

                            context.API.ShowMsg(message);

                            return false;
                        }
                    },
                    IcoPath = "Images\\copylink.png"
                }};
        }

        internal class BookmarkAttributes
        {
            internal string Url { get; set; }
        }
    }
}
