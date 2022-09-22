using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin.Explorer.Helper;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.Everything;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using Flow.Launcher.Plugin.Explorer.ViewModels;
using Flow.Launcher.Plugin.Explorer.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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


            // as at v1.7.0 this is to maintain backwards compatibility, need to be removed afterwards.
            if (Settings.QuickFolderAccessLinks.Any())
            {
                Settings.QuickAccessLinks = Settings.QuickFolderAccessLinks;
                Settings.QuickFolderAccessLinks = new();
            }

            contextMenu = new ContextMenu(Context, Settings, viewModel);
            searchManager = new SearchManager(Settings, Context);
            ResultManager.Init(Context, Settings);

            if (Settings.EverythingEnabled)
            {
                _ = EverythingDownloadHelper.PromptDownloadIfNotInstallAsync(Settings.EverythingInstalledPath, context.API)
                    .ContinueWith(s =>
                    {
                        if (s.IsCompletedSuccessfully)
                            Settings.EverythingInstalledPath = s.Result;
                    }, TaskScheduler.Default);
            }

            SortOptionTranslationHelper.API = context.API;

            EverythingApiDllImport.Load(Path.Combine(Context.CurrentPluginMetadata.PluginDirectory, "EverythingSDK",
                Environment.Is64BitProcess ? "Everything64.dll" : "Everything86.dll"));
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
            catch (Exception e) when (e is SearchException or SearchException)
            {
                return new List<Result>
                {
                    new()
                    {
                        Title = e.Message,
                        SubTitle = e is EngineNotAvailableException engineException
                            ? engineException.Resolution
                            : "Enter to copy the message to clipboard",
                        Score = 501,
                        IcoPath = Constants.ExplorerIconImagePath,
                        Action = _ =>
                        {
                            Clipboard.SetDataObject(e.ToString());
                            return true;
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
