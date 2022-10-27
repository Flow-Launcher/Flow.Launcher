using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Flow.Launcher.Pages
{
    public partial class Hotkey : Page
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
