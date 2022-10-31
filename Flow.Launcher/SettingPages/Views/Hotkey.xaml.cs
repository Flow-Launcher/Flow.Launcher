using System.Windows;

namespace Flow.Launcher.SettingPages.Views
{
    public partial class Hotkey
    {
        public Hotkey()
        {
            InitializeComponent();
        }


        private void OnDeleteCustomHotkeyClick(object sender, RoutedEventArgs e)
        {/*
            var item = viewModel.SelectedCustomPluginHotkey;
            if (item == null)
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("pleaseSelectAnItem"));
                return;
            }

            string deleteWarning =
                string.Format(InternationalizationManager.Instance.GetTranslation("deleteCustomHotkeyWarning"),
                    item.Hotkey);
            if (
                MessageBox.Show(deleteWarning, InternationalizationManager.Instance.GetTranslation("delete"),
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                settings.CustomPluginHotkeys.Remove(item);
                HotKeyMapper.RemoveHotkey(item.Hotkey);
            }
            */
        }

        private void OnnEditCustomHotkeyClick(object sender, RoutedEventArgs e)
        {
            /*
            var item = viewModel.SelectedCustomPluginHotkey;
            if (item != null)
            {
                CustomQueryHotkeySetting window = new CustomQueryHotkeySetting(this, settings);
                window.UpdateItem(item);
                window.ShowDialog();
            }
            else
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("pleaseSelectAnItem"));
            }
            */
        }

        private void OnAddCustomHotkeyClick(object sender, RoutedEventArgs e)
        {
          //  new CustomQueryHotkeySetting(this, settings).ShowDialog();
        }
    }
}
