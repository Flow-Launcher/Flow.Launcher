using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin.Explorer.ViewModels;
using Flow.Launcher.Plugin.Explorer.Views;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;

namespace Flow.Launcher.Plugin.Explorer
{
    class Main : ISettingProvider, IPlugin, ISavable, IPluginI18n, IContextMenu
    {
        private PluginInitContext Context { get; set; }

        private Settings _settings;

        private SettingsViewModel _viewModel;

        public Control CreateSettingPanel()
        {
            return new ExplorerSettings();
        }

        public string GetTranslatedPluginDescription()
        {
            throw new NotImplementedException();
        }

        public string GetTranslatedPluginTitle()
        {
            throw new NotImplementedException();
        }

        public void Init(PluginInitContext context)
        {
            Context = context;

            _viewModel = new SettingsViewModel();
            _settings = _viewModel.Settings;
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            throw new NotImplementedException();
        }

        public List<Result> Query(Query query)
        {
            return new List<Result>();
        }

        public void Save()
        {
            _viewModel.Save();
        }
    }
}
