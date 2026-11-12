using System.Windows.Controls;

namespace Flow.Launcher.Plugin.WebSearch
{
    public partial class Main
    {
        #region ISettingProvider Members

        public Control CreateSettingPanel()
        {
            return new SettingsControl(_context, _viewModel);
        }

        #endregion
    }
}
