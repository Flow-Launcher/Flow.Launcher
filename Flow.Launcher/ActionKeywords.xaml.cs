using System.Windows;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.Exception;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher
{
    public partial class ActionKeywords : Window
    {
        private readonly PluginPair plugin;
        private Settings settings;
        private readonly Internationalization translater = InternationalizationManager.Instance;
        private readonly PluginViewModel pluginViewModel;

        public ActionKeywords(string pluginId, Settings settings, PluginViewModel pluginViewModel)
        {
            InitializeComponent();
            plugin = PluginManager.GetPluginForId(pluginId);
            this.settings = settings;
            this.pluginViewModel = pluginViewModel;
            if (plugin == null)
            {
                MessageBox.Show(translater.GetTranslation("cannotFindSpecifiedPlugin"));
                Close();
            }
        }

        private void ActionKeyword_OnLoaded(object sender, RoutedEventArgs e)
        {
            tbOldActionKeyword.Text = string.Join(Query.ActionKeywordSeparator, plugin.Metadata.ActionKeywords.ToArray());
            tbAction.Focus();
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnDone_OnClick(object sender, RoutedEventArgs _)
        {
            var oldActionKeyword = plugin.Metadata.ActionKeywords[0];
            var newActionKeyword = tbAction.Text.Trim();
            newActionKeyword = newActionKeyword.Length > 0 ? newActionKeyword : "*";
            if (!pluginViewModel.IsActionKeywordRegistered(newActionKeyword))
            {
                pluginViewModel.ChangeActionKeyword(newActionKeyword, oldActionKeyword);
                Close();
            }
            else
            {
                string msg = translater.GetTranslation("newActionKeywordsHasBeenAssigned");
                MessageBox.Show(msg);
            }
        }
    }
}
