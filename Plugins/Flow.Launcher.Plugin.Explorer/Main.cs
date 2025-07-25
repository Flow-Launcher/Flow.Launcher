﻿using Flow.Launcher.Plugin.Explorer.Helper;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.Everything;
using Flow.Launcher.Plugin.Explorer.ViewModels;
using Flow.Launcher.Plugin.Explorer.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Plugin.Explorer.Exceptions;
using System.Linq;
using System.Globalization;

namespace Flow.Launcher.Plugin.Explorer
{
    public class Main : ISettingProvider, IAsyncPlugin, IContextMenu, IPluginI18n, IAsyncDialogJump
    {
        internal static PluginInitContext Context { get; set; }

        internal static Settings Settings { get; set; }

        private SettingsViewModel viewModel;

        private ContextMenu contextMenu;

        private SearchManager searchManager;

        private static readonly List<DialogJumpResult> _emptyDialogJumpResultList = new();

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
            contextMenu = new ContextMenu(Context, Settings);
            searchManager = new SearchManager(Settings, Context);
            ResultManager.Init(Context, Settings);

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

        public void OnCultureInfoChanged(CultureInfo newCulture)
        {
            // Update labels for setting view model
            EverythingSortOptionLocalized.UpdateLabels(viewModel.AllEverythingSortOptions);
        }

        private static void FillQuickAccessLinkNames()
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

        public async Task<List<DialogJumpResult>> QueryDialogJumpAsync(Query query, CancellationToken token)
        {
            try
            {
                var results = await searchManager.SearchAsync(query, token);
                return results.Select(r => DialogJumpResult.From(r, r.CopyText)).ToList();
            }
            catch (Exception e) when (e is SearchException or EngineNotAvailableException)
            {
                return _emptyDialogJumpResultList;
            }
        }
    }
}
