﻿using Flow.Launcher.Infrastructure.Storage;
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
            var search = query.Search.ToLower();

            var pluginManager = new PluginsManager(Context, Settings);

            if (!string.IsNullOrEmpty(search)
                    && ($"{Settings.HotkeyUninstall} ".StartsWith(search) || search.StartsWith($"{Settings.HotkeyUninstall} ")))
                return pluginManager.RequestUninstall(search);

            if (!string.IsNullOrEmpty(search)
                    && ($"{Settings.HotkeyUpdate} ".StartsWith(search) || search.StartsWith($"{Settings.HotkeyUpdate} ")))
                return pluginManager.RequestUpdate(search);

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
