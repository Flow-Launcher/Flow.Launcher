using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin.Explorer.Search.WindowsIndex;
using Flow.Launcher.Plugin.Explorer.ViewModels;
using Flow.Launcher.Plugin.Explorer.Views;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace Flow.Launcher.Plugin.Explorer
{
    class Main : ISettingProvider, IPlugin, ISavable, IContextMenu //, IPluginI18n <=== do later
    {
        private PluginInitContext Context { get; set; }

        private Settings _settings;

        private SettingsViewModel _viewModel;

        // Reserved keywords in oleDB
        private string ReservedStringPattern = @"^[\/\\\$\%]+$";

        public Control CreateSettingPanel()
        {
            return new ExplorerSettings();
        }

        public void Init(PluginInitContext context)
        {
            Context = context;

            _viewModel = new SettingsViewModel();
            _settings = _viewModel.Settings;
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            return new List<Result>();
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();

            if (string.IsNullOrEmpty(query.Search))
                return results;

            var regexMatch = Regex.Match(query.Search, ReservedStringPattern);

            if (regexMatch.Success)
                return results;

            var queryConstructor = new QueryConstructor(_settings);

            var searcher = new IndexSearcher(Context);

            return searcher.WindowsIndexSearch(query.Search,
                                               queryConstructor.CreateQueryHelper().ConnectionString,
                                               queryConstructor.QueryForAllFilesAndFolders);
        }

        public void Save()
        {
            _viewModel.Save();
        }
    }
}
