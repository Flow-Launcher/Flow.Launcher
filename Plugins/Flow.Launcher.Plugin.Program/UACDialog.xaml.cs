using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Flow.Launcher.Plugin.Program
{
    public partial class UACDialog : Window
    {
        private static readonly string ClassName = nameof(UACDialog);

        private static UACDialog msgBox;
        private static MessageBoxResult _result = MessageBoxResult.None;

        private UACDialog()
        {
            InitializeComponent();
        }

        public static MessageBoxResult Show(string iconPath, string appName, string fullPath)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                return Application.Current.Dispatcher.Invoke(() => Show(iconPath, appName, fullPath));
            }

            try
            {
                msgBox = new UACDialog
                {
                    Title = Main.Context.API.GetTranslation("flowlauncher_plugin_program_user_account_control_title")
                };
                
                // Set icon & app name & program location
                _ = msgBox.SetImageAsync(iconPath);
                msgBox.AppName.Text = appName;
                msgBox.ProgramLocation.Text = string.Format(
                    Main.Context.API.GetTranslation("flowlauncher_plugin_program_user_account_control_program_location"),
                    fullPath);

                // Focus No by default
                msgBox.btnNo.Focus();
                _result = MessageBoxResult.No;

                msgBox.ShowDialog();
                return _result;
            }
            catch (Exception e)
            {
                Main.Context.API.LogError(ClassName, $"An error occurred: {e.Message}");
                msgBox = null;
                return MessageBoxResult.None;
            }
        }

        private async Task SetImageAsync(string imagePath)
        {
            var imageSource = await Main.Context.API.LoadImageAsync(imagePath);
            Img.Source = imageSource;
        }

        private void KeyEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender == btnYes)
                _result = MessageBoxResult.Yes;
            else if (sender == btnNo)
                _result = MessageBoxResult.No;
            else
                _result = MessageBoxResult.None;
            msgBox.Close();
            msgBox = null;
        }

        private void Button_Cancel(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.Cancel;
            msgBox.Close();
            msgBox = null;
        }
    }
}
