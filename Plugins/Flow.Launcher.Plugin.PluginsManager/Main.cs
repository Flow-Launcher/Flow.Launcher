using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin.PluginsManager.ViewModels;
using Flow.Launcher.Plugin.PluginsManager.Views;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Flow.Launcher.Infrastructure;
using System;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.PluginsManager
{
    public class Main : ISettingProvider, IPlugin, ISavable, IContextMenu, IPluginI18n
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

        public void Init(PluginInitContext context)
        {
            Context = context;
            viewModel = new SettingsViewModel(context);
            Settings = viewModel.Settings;
            contextMenu = new ContextMenu(Context);
            pluginManager = new PluginsManager(Context, Settings);
            lastUpdateTime = DateTime.Now;
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            return contextMenu.LoadContextMenus(selectedResult);
        }

        public List<Result> Query(Query query)
        {
            var search = query.Search.ToLower();

            if (string.IsNullOrWhiteSpace(search))
                return pluginManager.GetDefaultHotKeys();

            if ((DateTime.Now - lastUpdateTime).TotalHours > 12) // 12 hours
            {
                Task.Run(async () =>
                {
                    await pluginManager.UpdateManifest();
                    lastUpdateTime = DateTime.Now;
                });
            }

            return search switch
            {
                var s when s.StartsWith(Settings.HotKeyInstall) => pluginManager.RequestInstallOrUpdate(s),
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
    }
}
