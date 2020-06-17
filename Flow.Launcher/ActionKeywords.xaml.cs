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
        private PluginPair _plugin;
        private Settings _settings;
        private readonly Internationalization _translater = InternationalizationManager.Instance;
        private readonly PluginViewModel pluginViewModel;

        public ActionKeywords(string pluginId, Settings settings, PluginViewModel pluginViewModel)
        {
            InitializeComponent();
            _plugin = PluginManager.GetPluginForId(pluginId);
            _settings = settings;
            this.pluginViewModel = pluginViewModel;
            {
                MessageBox.Show(_translater.GetTranslation("cannotFindSpecifiedPlugin"));
                Close();
            }
        }

        private void ActionKeyword_OnLoaded(object sender, RoutedEventArgs e)
        {
            tbOldActionKeyword.Text = string.Join(Query.ActionKeywordSeperater, _plugin.Metadata.ActionKeywords.ToArray());
            tbAction.Focus();
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnDone_OnClick(object sender, RoutedEventArgs _)
        {
            var oldActionKeyword = _plugin.Metadata.ActionKeywords[0];
            var newActionKeyword = tbAction.Text.Trim();
            newActionKeyword = newActionKeyword.Length > 0 ? newActionKeyword : "*";
            if (!pluginViewModel.IsActionKeywordRegistered(newActionKeyword))
            {
                pluginViewModel.ChangeActionKeyword(newActionKeyword, oldActionKeyword);
                Close();
            }
            else
            {
                string msg = _translater.GetTranslation("newActionKeywordsHasBeenAssigned");
                MessageBox.Show(msg);
            }
        }
    }
}
