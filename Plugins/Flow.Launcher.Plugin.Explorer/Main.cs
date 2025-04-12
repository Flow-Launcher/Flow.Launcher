using Flow.Launcher.Plugin.Explorer.Helper;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.Everything;
using Flow.Launcher.Plugin.Explorer.ViewModels;
using Flow.Launcher.Plugin.Explorer.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Plugin.Explorer.Exceptions;

namespace Flow.Launcher.Plugin.Explorer
{
    public class Main : ISettingProvider, IAsyncPlugin, IContextMenu, IPluginI18n
    {
        internal static PluginInitContext Context { get; set; }

        internal Settings Settings;

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
    }
}
