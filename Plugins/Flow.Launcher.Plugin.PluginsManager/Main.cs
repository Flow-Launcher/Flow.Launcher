using Flow.Launcher.Core.ExternalPlugins;
using Flow.Launcher.Plugin.PluginsManager.ViewModels;
using Flow.Launcher.Plugin.PluginsManager.Views;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Flow.Launcher.Infrastructure;
using System.Threading.Tasks;
using System.Threading;

namespace Flow.Launcher.Plugin.PluginsManager
{
    public class Main : ISettingProvider, IAsyncPlugin, IContextMenu, IPluginI18n
    {
        internal PluginInitContext Context { get; set; }

        internal Settings Settings;

        private SettingsViewModel viewModel;

        private IContextMenu contextMenu;

        internal PluginsManager pluginManager;

        public Control CreateSettingPanel()
        {
            return new PluginsManagerSettings(viewModel);
        }

        public async Task InitAsync(PluginInitContext context)
        {
            Context = context;
            Settings = context.API.LoadSettingJsonStorage<Settings>();
            viewModel = new SettingsViewModel(context, Settings);
            contextMenu = new ContextMenu(Context);
            pluginManager = new PluginsManager(Context, Settings);

            await PluginsManifest.UpdateManifestAsync();
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            return contextMenu.LoadContextMenus(selectedResult);
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(query.Search))
                return pluginManager.GetDefaultHotKeys();

            return query.FirstSearch.ToLower() switch
            {
                //search could be url, no need ToLower() when passed in
                Settings.InstallCommand => await pluginManager.RequestInstallOrUpdateAsync(query.SecondToEndSearch, token, query.IsReQuery),
                Settings.UninstallCommand => pluginManager.RequestUninstall(query.SecondToEndSearch),
                Settings.UpdateCommand => await pluginManager.RequestUpdateAsync(query.SecondToEndSearch, token, query.IsReQuery),
                _ => pluginManager.GetDefaultHotKeys().Where(hotkey =>
                {
                    hotkey.Score = StringMatcher.FuzzySearch(query.Search, hotkey.Title).Score;
                    return hotkey.Score > 0;
                }).ToList()
            };
        }

        public string GetTranslatedPluginTitle()
        {
            return Context.API.GetTranslation("plugin_pluginsmanager_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return Context.API.GetTranslation("plugin_pluginsmanager_plugin_description");
        }
    }
}
