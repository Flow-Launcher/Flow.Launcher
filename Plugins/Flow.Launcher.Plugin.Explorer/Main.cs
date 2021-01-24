using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.ViewModels;
using Flow.Launcher.Plugin.Explorer.Views;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Flow.Launcher.Plugin.Explorer
{
    public class Main : ISettingProvider, IAsyncPlugin, ISavable, IContextMenu, IPluginI18n
    {
        internal PluginInitContext Context { get; set; }

        internal Settings Settings;

        private SettingsViewModel viewModel;

        private IContextMenu contextMenu;

        private SearchManager searchManager;

        public Control CreateSettingPanel()
        {
            return new ExplorerSettings(viewModel);
        }

        public async Task InitAsync(PluginInitContext context)
        {
            Context = context;
            viewModel = new SettingsViewModel(context);
            await viewModel.LoadStorage();
            Settings = viewModel.Settings;
            contextMenu = new ContextMenu(Context, Settings);
            searchManager = new SearchManager(Settings, Context);
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            return contextMenu.LoadContextMenus(selectedResult);
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            return await searchManager.SearchAsync(query, token);
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
