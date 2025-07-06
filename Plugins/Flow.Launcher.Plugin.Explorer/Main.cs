﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Plugin.Explorer.Exceptions;
using Flow.Launcher.Plugin.Explorer.Helper;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.Everything;
using Flow.Launcher.Plugin.Explorer.ViewModels;
using Flow.Launcher.Plugin.Explorer.Views;

namespace Flow.Launcher.Plugin.Explorer
{
    public class Main : ISettingProvider, IAsyncPlugin, IContextMenu, IPluginI18n, IPluginHotkey
    {
        internal static PluginInitContext Context { get; set; }

        internal static Settings Settings { get; set; }

        private static readonly string ClassName = nameof(Main);

        private SettingsViewModel viewModel;

        private IContextMenu contextMenu;

        private SearchManager searchManager;

        public Control CreateSettingPanel()
        {
            return new ExplorerSettings(viewModel);
        }

        public Task InitAsync(PluginInitContext context)
        {
            Context = context;

            Settings = context.API.LoadSettingJsonStorage<Settings>();
            FillQuickAccessLinkNames();

            viewModel = new SettingsViewModel(context, Settings);

            contextMenu = new ContextMenu(Context, Settings, viewModel);
            searchManager = new SearchManager(Settings, Context);
            ResultManager.Init(Context, Settings);

            SortOptionTranslationHelper.API = context.API;

            EverythingApiDllImport.Load(Path.Combine(Context.CurrentPluginMetadata.PluginDirectory, "EverythingSDK",
                Environment.Is64BitProcess ? "x64" : "x86"));

            return Task.CompletedTask;
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            return contextMenu.LoadContextMenus(selectedResult);
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            try
            {
                return await searchManager.SearchAsync(query, token);
            }
            catch (Exception e) when (e is SearchException or EngineNotAvailableException)
            {
                return new List<Result>
                {
                    new()
                    {
                        Title = e.Message,
                        SubTitle = e is EngineNotAvailableException { Resolution: { } resolution }
                            ? resolution
                            : "Enter to copy the message to clipboard",
                        Score = 501,
                        IcoPath = e is EngineNotAvailableException { ErrorIcon: { } iconPath }
                            ? iconPath
                            : Constants.GeneralSearchErrorImagePath,
                        AsyncAction = e is EngineNotAvailableException {Action: { } action}
                            ? action
                            : _ =>
                            {
                                Context.API.CopyToClipboard(e.ToString());
                                return new ValueTask<bool>(true);
                            }
                    }
                };
            }
        }

        public string GetTranslatedPluginTitle()
        {
            return Context.API.GetTranslation("plugin_explorer_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return Context.API.GetTranslation("plugin_explorer_plugin_description");
        }

        private void FillQuickAccessLinkNames()
        {
            // Legacy version does not have names for quick access links, so we fill them with the path name.
            foreach (var link in Settings.QuickAccessLinks)
            {
                if (string.IsNullOrWhiteSpace(link.Name))
                {
                    link.Name = link.Path.GetPathName();
                }
            }
        }

        public List<BasePluginHotkey> GetPluginHotkeys()
        {
            return new List<BasePluginHotkey>
            {
                new SearchWindowPluginHotkey()
                {
                    Id = 0,
                    Name = Context.API.GetTranslation("plugin_explorer_opencontainingfolder"),
                    Description = Context.API.GetTranslation("plugin_explorer_opencontainingfolder_subtitle"),
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue838"),
                    DefaultHotkey = "Ctrl+Enter",
                    Editable = false,
                    Visible = true,
                    Action = (r) =>
                    {
                        if (r.ContextData is SearchResult record)
                        {
                            if (record.Type is ResultType.File)
                            {
                                ResultManager.OpenFolder(record.FullPath, record.FullPath);
                            }
                            else
                            {
                                try
                                {
                                    Context.API.OpenDirectory(Path.GetDirectoryName(record.FullPath), record.FullPath);
                                }
                                catch (Exception e)
                                {
                                    var message = $"Fail to open file at {record.FullPath}";
                                    Context.API.LogException(ClassName, message, e);
                                    Context.API.ShowMsgBox(e.Message, Context.API.GetTranslation("plugin_explorer_opendir_error"));
                                    return false;
                                }

                                return true;
                            }
                        }

                        return false;
                    }
                },
                new SearchWindowPluginHotkey()
                {
                    Id = 1,
                    Name = Context.API.GetTranslation("plugin_explorer_show_contextmenu_title"),
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue700"),
                    DefaultHotkey = "Alt+Enter",
                    Editable = false,
                    Visible = true,
                    Action = (r) =>
                    {
                        if (r.ContextData is SearchResult record && record.Type is not ResultType.Volume)
                        {
                            try
                            {
                                ResultManager.ShowNativeContextMenu(record.FullPath, record.Type);
                            }
                            catch (Exception e)
                            {
                                var message = $"Fail to show context menu for {record.FullPath}";
                                Context.API.LogException(ClassName, message, e);
                            }
                        }

                        return false;
                    }
                },
                new SearchWindowPluginHotkey()
                {
                    Id = 2,
                    Name = Context.API.GetTranslation("plugin_explorer_run_as_administrator"),
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\uE7EF"),
                    DefaultHotkey = "Ctrl+Shift+Enter",
                    Editable = false,
                    Visible = true,
                    Action = (r) =>
                    {
                        if (r.ContextData is SearchResult record)
                        {
                            if (record.Type is ResultType.File)
                            {
                                var filePath = record.FullPath;
                                ResultManager.OpenFile(filePath, Settings.UseLocationAsWorkingDir ? Path.GetDirectoryName(filePath) : string.Empty, true);
                            }
                            else
                            {
                                try
                                {
                                    ResultManager.OpenFolder(record.FullPath);
                                    return true;
                                }
                                catch (Exception ex)
                                {
                                    var message = $"Fail to open file at {record.FullPath}";
                                    Context.API.LogException(ClassName, message, ex);
                                    Context.API.ShowMsgBox(ex.Message, Context.API.GetTranslation("plugin_explorer_opendir_error"));
                                    return false;
                                }
                            }
                            return true;
                        }

                        return false;
                    }
                },
                new SearchWindowPluginHotkey()
                {
                    Id = 3,
                    Name = Context.API.GetTranslation("plugin_explorer_rename_a_file"),
                    Description = Context.API.GetTranslation("plugin_explorer_rename_subtitle"),
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue8ac"),
                    DefaultHotkey = "F2",
                    Editable = true,
                    Visible = true,
                    Action = (r) =>
                    {
                        if (r.ContextData is SearchResult record)
                        {
                            RenameFile window;
                            switch (record.Type)
                            {
                                case ResultType.Folder:
                                    window = new RenameFile(Context.API, new DirectoryInfo(record.FullPath));
                                    break;
                                case ResultType.File:
                                    window = new RenameFile(Context.API, new FileInfo(record.FullPath));
                                    break;
                                default:
                                    Context.API.ShowMsgError(Context.API.GetTranslation("plugin_explorer_cannot_rename"));
                                    return false;
                            }
                            window.ShowDialog();

                            return false;
                        }

                        return false;
                    }
                }
            };
        }
    }
}
