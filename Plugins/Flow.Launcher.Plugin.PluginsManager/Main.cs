using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin.PluginsManager.ViewModels;
using Flow.Launcher.Plugin.PluginsManager.Views;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Flow.Launcher.Infrastructure;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;

namespace Flow.Launcher.Plugin.PluginsManager
{
    public class Main : ISettingProvider, IAsyncPlugin, IContextMenu, IPluginI18n, IAsyncReloadable
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

        public Task InitAsync(PluginInitContext context)
        {
            Context = context;
            Settings = context.API.LoadSettingJsonStorage<Settings>();
            viewModel = new SettingsViewModel(context, Settings);
            contextMenu = new ContextMenu(Context);
            pluginManager = new PluginsManager(Context, Settings);

            return Task.CompletedTask;
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            return contextMenu.LoadContextMenus(selectedResult);
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            var search = query.Search;

            if (string.IsNullOrWhiteSpace(search))
                return pluginManager.GetDefaultHotKeys();

            return search switch
            {
                //search could be url, no need ToLower() when passed in
                var s when s.StartsWith(Settings.HotKeyInstall, StringComparison.OrdinalIgnoreCase)
                    => await pluginManager.RequestInstallOrUpdate(search, token),
                var s when s.StartsWith(Settings.HotkeyUninstall, StringComparison.OrdinalIgnoreCase)
                    => pluginManager.RequestUninstall(search),
                var s when s.StartsWith(Settings.HotkeyUpdate, StringComparison.OrdinalIgnoreCase)
                    => await pluginManager.RequestUpdate(search, token),
                _ => pluginManager.GetDefaultHotKeys().Where(hotkey =>
                {
                    hotkey.Score = StringMatcher.FuzzySearch(search, hotkey.Title).Score;
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

        public async Task ReloadDataAsync()
        {
            await pluginManager.UpdateManifestAsync();
        }
    }
}