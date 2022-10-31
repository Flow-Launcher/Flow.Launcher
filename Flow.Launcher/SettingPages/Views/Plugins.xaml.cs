using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;

namespace Flow.Launcher.SettingPages.Views
{
    /// <summary>
    /// Plugins.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Plugins
    {
        public Plugins()
        {

            InitializeComponent();
        }
        private void OnLoaded(object sender, RoutedEventArgs e)
        {

         //   pluginListView = (CollectionView)CollectionViewSource.GetDefaultView(Plugins.ItemsSource);
//            pluginListView.Filter = PluginListFilter;

  //          pluginStoreView = (CollectionView)CollectionViewSource.GetDefaultView(StoreListView.ItemsSource);
            //pluginStoreView.Filter = PluginStoreFilter;

            //InitializePosition();
        }

        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            /*
            API.OpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
            */
        }
        private void OnPluginToggled(object sender, RoutedEventArgs e)
        {
            /*
            var id = viewModel.SelectedPlugin.PluginPair.Metadata.ID;
            // used to sync the current status from the plugin manager into the setting to keep consistency after save
            settings.PluginSettings.Plugins[id].Disabled = viewModel.SelectedPlugin.PluginPair.Metadata.Disabled;
            */
        }

        private void OnPluginPriorityClick(object sender, RoutedEventArgs e)
        {
            /*
            if (sender is Control { DataContext: PluginViewModel pluginViewModel })
            {
                PriorityChangeWindow priorityChangeWindow = new PriorityChangeWindow(pluginViewModel.PluginPair.Metadata.ID, settings, pluginViewModel);
                priorityChangeWindow.ShowDialog();
            }
            */
        }

        private void OnPluginActionKeywordsClick(object sender, RoutedEventArgs e)
        {
            /*
            var id = viewModel.SelectedPlugin.PluginPair.Metadata.ID;
            ActionKeywords changeKeywordsWindow = new ActionKeywords(id, settings, viewModel.SelectedPlugin);
            changeKeywordsWindow.ShowDialog();
            */
        }

        private void OnPluginNameClick(object sender, MouseButtonEventArgs e)
        {
            /*
            if (e.ChangedButton == MouseButton.Left)
            {
                var website = viewModel.SelectedPlugin.PluginPair.Metadata.Website;
                if (!string.IsNullOrEmpty(website))
                {
                    var uri = new Uri(website);
                    if (Uri.CheckSchemeName(uri.Scheme))
                    {
                        website.OpenInBrowserTab();
                    }
                }
            }
            */
        }

        private void OnPluginDirecotyClick(object sender, MouseButtonEventArgs e)
        {
            /*
            if (e.ChangedButton == MouseButton.Left)
            {
                var directory = viewModel.SelectedPlugin.PluginPair.Metadata.PluginDirectory;
                if (!string.IsNullOrEmpty(directory))
                    PluginManager.API.OpenDirectory(directory);
            }
            */
        }

        private void OnExternalPluginUninstallClick(object sender, MouseButtonEventArgs e)
        {
        //    if (e.ChangedButton == MouseButton.Left)
        //    {
        //        var id = viewModel.SelectedPlugin.PluginPair.Metadata.Name;
        //        var pluginsManagerPlugin = PluginManager.GetPluginForId("9f8f9b14-2518-4907-b211-35ab6290dee7");
        //        var actionKeyword = pluginsManagerPlugin.Metadata.ActionKeywords.Count == 0 ? "" : pluginsManagerPlugin.Metadata.ActionKeywords[0];
        //        API.ChangeQuery($"{actionKeyword} uninstall {id}");
        //        API.ShowMainWindow();
        //    }

        }

        private bool PluginListFilter(object item)
        {
        //    if (string.IsNullOrEmpty(pluginFilterTxb.Text))
        //        return true;
        //    if (item is PluginViewModel model)
        //    {
        //        return StringMatcher.FuzzySearch(pluginFilterTxb.Text, model.PluginPair.Metadata.Name).IsSearchPrecisionScoreMet();
        //    }
            return false;
        }
        private void RefreshPluginListEventHandler(object sender, RoutedEventArgs e)
        {
        //    if (pluginFilterTxb.Text != lastPluginListSearch)
        //    {
        //        lastPluginListSearch = pluginFilterTxb.Text;
        //        pluginListView.Refresh();
        //    }
        }

        private void PluginFilterTxb_OnKeyDown(object sender, KeyEventArgs e)
        {
        //    if (e.Key == Key.Enter)
        //        RefreshPluginListEventHandler(sender, e);
        }
    }
}
