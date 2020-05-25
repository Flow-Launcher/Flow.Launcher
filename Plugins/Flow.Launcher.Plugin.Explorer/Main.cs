using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.ViewModels;
using Flow.Launcher.Plugin.Explorer.Views;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace Flow.Launcher.Plugin.Explorer
{
    public class Main : ISettingProvider, IPlugin, ISavable, IContextMenu //, IPluginI18n <=== do later
    {
        internal static PluginInitContext Context { get; set; }

        private Settings _settings;

        private SettingsViewModel _viewModel;

        private IContextMenu _contextMenu;

        public Control CreateSettingPanel()
        {
            return new ExplorerSettings();
        }

        public void Init(PluginInitContext context)
        {
            Context = context;
            _viewModel = new SettingsViewModel();
            _settings = _viewModel.Settings;
            _contextMenu = new ContextMenu();
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            return _contextMenu.LoadContextMenus(selectedResult);
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();

            if (string.IsNullOrEmpty(query.Search))
                return results;

            return new SearchManager(_settings, Context).Search(query);
        }

        public void Save()
        {
            _viewModel.Save();
        }
    }
}
