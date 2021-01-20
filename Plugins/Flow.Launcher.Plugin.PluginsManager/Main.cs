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

namespace Flow.Launcher.Plugin.PluginsManager
{
    public class Main : ISettingProvider, IAsyncPlugin, ISavable, IContextMenu, IPluginI18n, IAsyncReloadable
    {
        internal PluginInitContext Context { get; set; }

        internal Settings Settings;

        private SettingsViewModel viewModel;

        private IContextMenu contextMenu;

        internal PluginsManager pluginManager;

        private DateTime lastUpdateTime = DateTime.MinValue;

        public Control CreateSettingPanel()
        {
            return new PluginsManagerSettings(viewModel);
        }

        public Task InitAsync(PluginInitContext context)
        {
            Context = context;
            viewModel = new SettingsViewModel(context);
            Settings = viewModel.Settings;
            contextMenu = new ContextMenu(Context);
            pluginManager = new PluginsManager(Context, Settings);
            var updateManifestTask = pluginManager.UpdateManifest();
            _ = updateManifestTask.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                    lastUpdateTime = DateTime.Now;
                else
                {
                    context.API.ShowMsg("Plugin Manifest Download Fail.",
                    "Please check if you can connect to github.com. " +
                    "This error means you may not be able to Install and Update Plugin.", pluginManager.icoPath, false);
                }
            });

            return Task.CompletedTask;
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            return contextMenu.LoadContextMenus(selectedResult);
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            var search = query.Search.ToLower();

            if (string.IsNullOrWhiteSpace(search))
                return pluginManager.GetDefaultHotKeys();

            if ((DateTime.Now - lastUpdateTime).TotalHours > 12) // 12 hours
            {
                await pluginManager.UpdateManifest();
                lastUpdateTime = DateTime.Now;
            }

            return search switch
            {
                var s when s.StartsWith(Settings.HotKeyInstall) => await pluginManager.RequestInstallOrUpdate(s, token),
                var s when s.StartsWith(Settings.HotkeyUninstall) => pluginManager.RequestUninstall(s),
                var s when s.StartsWith(Settings.HotkeyUpdate) => pluginManager.RequestUpdate(s),
                _ => pluginManager.GetDefaultHotKeys().Where(hotkey =>
                {
                    hotkey.Score = StringMatcher.FuzzySearch(search, hotkey.Title).Score;
                    return hotkey.Score > 0;
                }).ToList()
            };
        }

        public void Save()
        {
            viewModel.Save();
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
            await pluginManager.UpdateManifest();
            lastUpdateTime = DateTime.Now;
        }
    }
}