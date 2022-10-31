using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Navigation;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.SettingPages.Views
{
    /// <summary>
    /// PluginStore.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PluginStore
    {
        public PluginStore()
        {
            InitializeComponent();
        }

        private CollectionView pluginListView;
        private CollectionView pluginStoreView;

        private bool PluginListFilter(object item)
        {
/*            if (string.IsNullOrEmpty(pluginFilterTxb.Text))
                return true;
            if (item is PluginViewModel model)
            {
                return StringMatcher.FuzzySearch(pluginFilterTxb.Text, model.PluginPair.Metadata.Name).IsSearchPrecisionScoreMet();
            }*/
            return false;
        }

        private bool PluginStoreFilter(object item)
        {
/*            if (string.IsNullOrEmpty(pluginStoreFilterTxb.Text))
                return true;
            if (item is UserPlugin model)
            {
                return StringMatcher.FuzzySearch(pluginStoreFilterTxb.Text, model.Name).IsSearchPrecisionScoreMet()
                    || StringMatcher.FuzzySearch(pluginStoreFilterTxb.Text, model.Description).IsSearchPrecisionScoreMet();
            }*/
            return false;
        }

        private string lastPluginListSearch = "";
        private string lastPluginStoreSearch = "";

        private void RefreshPluginListEventHandler(object sender, RoutedEventArgs e)
        {
/*            if (pluginFilterTxb.Text != lastPluginListSearch)
            {
                lastPluginListSearch = pluginFilterTxb.Text;
                pluginListView.Refresh();
            }*/
        }

        private void RefreshPluginStoreEventHandler(object sender, RoutedEventArgs e)
        {
            if (pluginStoreFilterTxb.Text != lastPluginStoreSearch)
            {
                lastPluginStoreSearch = pluginStoreFilterTxb.Text;
                pluginStoreView.Refresh();
            }
        }

        private void PluginFilterTxb_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                RefreshPluginListEventHandler(sender, e);
        }

        private void PluginStoreFilterTxb_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                RefreshPluginStoreEventHandler(sender, e);
        }

        private void OnPluginSettingKeydown(object sender, KeyEventArgs e)
        {
            /*if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.F)
                pluginFilterTxb.Focus();*/
        }

        private void PluginStore_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                pluginStoreFilterTxb.Focus();
            }
        }

        private void OnPluginStoreRefreshClick(object sender, RoutedEventArgs e)
        {
            /*_ = viewModel.RefreshExternalPluginsAsync();*/
        }

        private void OnExternalPluginInstallClick(object sender, RoutedEventArgs e)
        {
/*            if (sender is Button { DataContext: UserPlugin plugin })
            {
                var pluginsManagerPlugin = PluginManager.GetPluginForId("9f8f9b14-2518-4907-b211-35ab6290dee7");
                var actionKeyword = pluginsManagerPlugin.Metadata.ActionKeywords.Count == 0 ? "" : pluginsManagerPlugin.Metadata.ActionKeywords[0];
                API.ChangeQuery($"{actionKeyword} install {plugin.Name}");
                API.ShowMainWindow();
            }*/
        }

        private void OnExternalPluginUninstallClick(object sender, RoutedEventArgs e)
        {
            /*
            if (sender is Button { DataContext: PluginStoreItemViewModel plugin })
                viewModel.DisplayPluginQuery($"uninstall {plugin.Name}", PluginManager.GetPluginForId("9f8f9b14-2518-4907-b211-35ab6290dee7"));
            */
        }

        private void OnExternalPluginUpdateClick(object sender, RoutedEventArgs e)
        {
            /*
            if (sender is Button { DataContext: PluginStoreItemViewModel plugin })
                viewModel.DisplayPluginQuery($"update {plugin.Name}", PluginManager.GetPluginForId("9f8f9b14-2518-4907-b211-35ab6290dee7"));
            */
        }
        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
/*            API.OpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;*/
        }
    }
}
