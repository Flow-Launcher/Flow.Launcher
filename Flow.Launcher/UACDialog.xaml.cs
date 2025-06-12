using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Flow.Launcher
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

        public static MessageBoxResult Show(string appName, string iconPath, string fullPath)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                return Application.Current.Dispatcher.Invoke(() => Show(appName, iconPath, fullPath));
            }

            try
            {
                msgBox = new UACDialog();
                
                // Set icon & app name & program location
                _ = msgBox.SetImageAsync(iconPath);
                msgBox.AppName.Text = appName;
                if (string.IsNullOrEmpty(fullPath))
                {
                    msgBox.ProgramLocation.Text = string.Format(
                        App.API.GetTranslation("userAccountControlProgramLocation"),
                        appName);
                }
                else
                {
                    msgBox.ProgramLocation.Text = string.Format(
                        App.API.GetTranslation("userAccountControlProgramLocation"),
                        fullPath);
                }

                // Focus No by default
                msgBox.btnNo.Focus();
                _result = MessageBoxResult.No;

                msgBox.ShowDialog();
                return _result;
            }
            catch (Exception e)
            {
                App.API.LogError(ClassName, $"An error occurred: {e.Message}");
                msgBox = null;
                return MessageBoxResult.None;
            }
        }

        private async Task SetImageAsync(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                Img.Visibility = Visibility.Collapsed;
                return;
            }
            var imageSource = await App.API.LoadImageAsync(imagePath);
            Img.Source = imageSource;
        }

        private void KeyEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
        {
            _result = MessageBoxResult.Cancel;
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
