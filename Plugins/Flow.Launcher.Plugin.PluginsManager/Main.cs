using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.PluginsManager.ViewModels;
using Flow.Launcher.Plugin.PluginsManager.Views;
using System.Collections.Generic;
using System.Windows.Controls;

namespace Flow.Launcher.Plugin.PluginsManager
{
    public class Main : ISettingProvider, IPlugin, ISavable, IContextMenu, IPluginI18n
    {
        internal PluginInitContext Context { get; set; }

        internal Settings Settings;

        private SettingsViewModel viewModel;

        private IContextMenu contextMenu;

        public Control CreateSettingPanel()
        {
            return new PluginsManagerSettings(viewModel);
        }

        public void Init(PluginInitContext context)
        {
            Context = context;
            viewModel = new SettingsViewModel(context);
            Settings = viewModel.Settings;
            contextMenu = new ContextMenu(Context, Settings);
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            return contextMenu.LoadContextMenus(selectedResult);
        }

        public List<Result> Query(Query query)
        {
            var search = query.Search;

            var uninstallCmd = "uninstall";

            var pluginManager = new PluginsManager(Context);
            
            if (search.ToLower().StartsWith($"{uninstallCmd} ") || search.ToLower() == uninstallCmd)
                return pluginManager.RequestUninstall(search.Replace(uninstallCmd, string.Empty).Trim());
            
            return pluginManager.RequestInstallOrUpdate(search);
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
