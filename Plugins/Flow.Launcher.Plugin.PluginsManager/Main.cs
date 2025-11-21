using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Plugin.PluginsManager.ViewModels;
using Flow.Launcher.Plugin.PluginsManager.Views;

namespace Flow.Launcher.Plugin.PluginsManager
{
    public class Main : ISettingProvider, IAsyncPlugin, IContextMenu, IPluginI18n, IPluginHotkey
    {
        internal static PluginInitContext Context { get; set; }

        internal Settings Settings;

        private SettingsViewModel viewModel;

        private IContextMenu contextMenu;

        internal PluginsManager pluginManager;

        public Control CreateSettingPanel()
        {
            return new PluginsManagerSettings(viewModel);
        }

        public async Task InitAsync(PluginInitContext context)
        {
            Context = context;
            Settings = context.API.LoadSettingJsonStorage<Settings>();
            viewModel = new SettingsViewModel(context, Settings);
            contextMenu = new ContextMenu(Context);
            pluginManager = new PluginsManager(Context, Settings);

            await Context.API.UpdatePluginManifestAsync();
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            return contextMenu.LoadContextMenus(selectedResult);
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(query.Search))
                return pluginManager.GetDefaultHotKeys();

            return query.FirstSearch.ToLower() switch
            {
                //search could be url, no need ToLower() when passed in
                Settings.InstallCommand => await pluginManager.RequestInstallOrUpdateAsync(query.SecondToEndSearch, token, query.IsReQuery),
                Settings.UninstallCommand => pluginManager.RequestUninstall(query.SecondToEndSearch),
                Settings.UpdateCommand => await pluginManager.RequestUpdateAsync(query.SecondToEndSearch, token, query.IsReQuery),
                _ => pluginManager.GetDefaultHotKeys().Where(hotkey =>
                {
                    hotkey.Score = Context.API.FuzzySearch(query.Search, hotkey.Title).Score;
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

        public List<BasePluginHotkey> GetPluginHotkeys()
        {
            return new List<BasePluginHotkey>
            {
                new SearchWindowPluginHotkey
                {
                    Id = 0,
                    Name = Context.API.GetTranslation("plugin_pluginsmanager_plugin_contextmenu_openwebsite_title"),
                    Description = Context.API.GetTranslation("plugin_pluginsmanager_plugin_contextmenu_openwebsite_subtitle"),
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\uEB41"),
                    DefaultHotkey = "Ctrl+Enter",
                    Editable = false,
                    Visible = true,
                    Action = (r) =>
                    {
                        if (r.ContextData is UserPlugin plugin)
                        {
                            if (!string.IsNullOrWhiteSpace(plugin.Website))
                            {
                                Context.API.OpenUrl(plugin.Website);

                                return true;
                            }
                        }

                        return false;
                    }
                }
            };
        }
    }
}
