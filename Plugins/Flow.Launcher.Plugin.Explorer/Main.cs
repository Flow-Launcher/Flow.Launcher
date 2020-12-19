using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.ViewModels;
using Flow.Launcher.Plugin.Explorer.Views;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Controls;

namespace Flow.Launcher.Plugin.Explorer
{
    public class Main : ISettingProvider, IPlugin, ISavable, IContextMenu, IPluginI18n
    {
        internal PluginInitContext Context { get; set; }

        internal Settings Settings;

        private SettingsViewModel viewModel;

        private IContextMenu contextMenu;

        private static CancellationTokenSource updateSource;
        public static CancellationToken updateToken;

        public Control CreateSettingPanel()
        {
            return new ExplorerSettings(viewModel);
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
            updateSource?.Cancel();
            updateSource = new CancellationTokenSource();
            updateToken = updateSource.Token;
            return new SearchManager(Settings, Context).Search(query);
        }

        public void Save()
        {
            viewModel.Save();
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
