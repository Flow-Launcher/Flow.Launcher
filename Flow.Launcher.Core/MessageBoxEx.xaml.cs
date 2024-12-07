using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Core
{
    public partial class MessageBoxEx : Window
    {
        private static MessageBoxEx msgBox;
        private static MessageBoxResult _result = MessageBoxResult.None;

        private readonly MessageBoxButton _button;

        private MessageBoxEx(MessageBoxButton button)
        {
            _button = button;
            InitializeComponent();
        }

        public static MessageBoxResult Show(string messageBoxText)
            => Show(messageBoxText, string.Empty, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.OK);

        public static MessageBoxResult Show(
            string messageBoxText,
            string caption = "",
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.OK)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                return Application.Current.Dispatcher.Invoke(() => Show(messageBoxText, caption, button, icon, defaultResult));
            }

            try
            {
                msgBox = new MessageBoxEx(button);
                if (caption == string.Empty && button == MessageBoxButton.OK && icon == MessageBoxImage.None)
                {
                    msgBox.Title = messageBoxText;
                    msgBox.DescOnlyTextBlock.Visibility = Visibility.Visible;
                    msgBox.DescOnlyTextBlock.Text = messageBoxText;
                }
                else
                {
                    msgBox.Title = caption;
                    msgBox.TitleTextBlock.Text = caption;
                    msgBox.DescTextBlock.Text = messageBoxText;
                    _ = SetImageOfMessageBoxAsync(icon);
                }
                SetButtonVisibilityFocusAndResult(button, defaultResult);
                msgBox.ShowDialog();
                return _result;
            }
            catch (Exception e)
            {
                Log.Error($"|MessageBoxEx.Show|An error occurred: {e.Message}");
                msgBox = null;
                return MessageBoxResult.None;
            }
        }

        private static void SetButtonVisibilityFocusAndResult(MessageBoxButton button, MessageBoxResult defaultResult)
        {
            switch (button)
            {
                case MessageBoxButton.OK:
                    msgBox.btnCancel.Visibility = Visibility.Collapsed;
                    msgBox.btnNo.Visibility = Visibility.Collapsed;
                    msgBox.btnYes.Visibility = Visibility.Collapsed;
                    msgBox.btnOk.Focus();
                    _result = MessageBoxResult.OK;
                    break;
                case MessageBoxButton.OKCancel:
                    msgBox.btnNo.Visibility = Visibility.Collapsed;
                    msgBox.btnYes.Visibility = Visibility.Collapsed;
                    if (defaultResult == MessageBoxResult.Cancel)
                    {
                        msgBox.btnCancel.Focus();
                        _result = MessageBoxResult.Cancel;
                    }
                    else
                    {
                        msgBox.btnOk.Focus();
                        _result = MessageBoxResult.OK;
                    }
                    break;
                case MessageBoxButton.YesNo:
                    msgBox.btnOk.Visibility = Visibility.Collapsed;
                    msgBox.btnCancel.Visibility = Visibility.Collapsed;
                    if (defaultResult == MessageBoxResult.No)
                    {
                        msgBox.btnNo.Focus();
                        _result = MessageBoxResult.No;
                    }
                    else
                    {
                        msgBox.btnYes.Focus();
                        _result = MessageBoxResult.Yes;
                    }
                    break;
                case MessageBoxButton.YesNoCancel:
                    msgBox.btnOk.Visibility = Visibility.Collapsed;
                    if (defaultResult == MessageBoxResult.No)
                    {
                        msgBox.btnNo.Focus();
                        _result = MessageBoxResult.No;
                    }
                    else if (defaultResult == MessageBoxResult.Cancel)
                    {
                        msgBox.btnCancel.Focus();
                        _result = MessageBoxResult.Cancel;
                    }
                    else
                    {
                        msgBox.btnYes.Focus();
                        _result = MessageBoxResult.Yes;
                    }
                    break;
                default:
                    break;
            }
        }

        private static async Task SetImageOfMessageBoxAsync(MessageBoxImage icon)
        {
            switch (icon)
            {
                case MessageBoxImage.Exclamation:
                    await msgBox.SetImageAsync("Exclamation.png");
                    msgBox.Img.Visibility = Visibility.Visible;
                    break;
                case MessageBoxImage.Question:
                    await msgBox.SetImageAsync("Question.png");
                    msgBox.Img.Visibility = Visibility.Visible;
                    break;
                case MessageBoxImage.Information:
                    await msgBox.SetImageAsync("Information.png");
                    msgBox.Img.Visibility = Visibility.Visible;
                    break;
                case MessageBoxImage.Error:
                    await msgBox.SetImageAsync("Error.png");
                    msgBox.Img.Visibility = Visibility.Visible;
                    break;
                default:
                    msgBox.Img.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private async Task SetImageAsync(string imageName)
        {
            var imagePath = Path.Combine(Constant.ProgramDirectory, "Images", imageName);
            var imageSource = await ImageLoader.LoadAsync(imagePath);
            Img.Source = imageSource;
        }

        private void KeyEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
        {
            if (_button == MessageBoxButton.YesNo)
                return;
            else if (_button == MessageBoxButton.OK)
                _result = MessageBoxResult.OK;
            else
                _result = MessageBoxResult.Cancel;
            DialogResult = false;
            Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender == btnOk)
                _result = MessageBoxResult.OK;
            else if (sender == btnYes)
                _result = MessageBoxResult.Yes;
            else if (sender == btnNo)
                _result = MessageBoxResult.No;
            else if (sender == btnCancel)
                _result = MessageBoxResult.Cancel;
            else
                _result = MessageBoxResult.None;
            msgBox.Close();
            msgBox = null;
        }

        private void Button_Cancel(object sender, RoutedEventArgs e)
        {
            if (_button == MessageBoxButton.YesNo)
                return;
            else if (_button == MessageBoxButton.OK)
                _result = MessageBoxResult.OK;
            else
                _result = MessageBoxResult.Cancel;
            msgBox.Close();
            msgBox = null;
        }
    }
}
