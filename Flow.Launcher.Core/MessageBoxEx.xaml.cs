using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Flow.Launcher.Infrastructure;

namespace Flow.Launcher.Core
{
    public partial class MessageBoxEx : Window
    {
        public MessageBoxEx()
        {
            InitializeComponent();
        }

        static MessageBoxEx msgBox;
        static MessageBoxResult _result = MessageBoxResult.No;

        /// 1 parameter
        public static MessageBoxResult Show(string messageBoxText)
        {
            return Show(messageBoxText, string.Empty, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.OK);
        }

        // 2 parameter
        public static MessageBoxResult Show(string messageBoxText, string caption)
        {
            return Show(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.OK);
        }

        /// 3 parameter
        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button) 
        {
            return Show(messageBoxText, caption, button, MessageBoxImage.None, MessageBoxResult.OK);
        }

        // 4 parameter
        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            return Show(messageBoxText, caption, button, icon, MessageBoxResult.OK);
        }

        // 5 parameter, Final Display Message. 
        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
        {
            msgBox = new MessageBoxEx();
            if (caption == string.Empty && button == MessageBoxButton.OK && icon == MessageBoxImage.None)
            {
                msgBox.DescOnlyTextBlock.Text = messageBoxText;
                msgBox.Title = messageBoxText;
            }
            else
            {
                msgBox.TitleTextBlock.Text = caption;
                msgBox.DescTextBlock.Text = messageBoxText;
                msgBox.Title = caption;
                SetImageOfMessageBox(icon);
            }
            SetVisibilityOfButtons(button, defaultResult);
            msgBox.ShowDialog();
            return _result;
        }

        private static void SetVisibilityOfButtons(MessageBoxButton button, MessageBoxResult defaultResult)
        {
            switch (button)
            {
                case MessageBoxButton.OK:
                    msgBox.btnCancel.Visibility = Visibility.Collapsed;
                    msgBox.btnNo.Visibility = Visibility.Collapsed;
                    msgBox.btnYes.Visibility = Visibility.Collapsed;
                    msgBox.btnOk.Focus();
                    break;
                case MessageBoxButton.OKCancel:
                    msgBox.btnNo.Visibility = Visibility.Collapsed;
                    msgBox.btnYes.Visibility = Visibility.Collapsed;
                    if (defaultResult == MessageBoxResult.Cancel)
                        msgBox.btnCancel.Focus();
                    else
                        msgBox.btnOk.Focus();
                    break;
                case MessageBoxButton.YesNo:
                    msgBox.btnOk.Visibility = Visibility.Collapsed;
                    msgBox.btnCancel.Visibility = Visibility.Collapsed;
                    if (defaultResult == MessageBoxResult.No)
                        msgBox.btnNo.Focus();
                    else
                        msgBox.btnYes.Focus();
                    break;
                case MessageBoxButton.YesNoCancel:
                    msgBox.btnOk.Visibility = Visibility.Collapsed;
                    if (defaultResult == MessageBoxResult.No)
                        msgBox.btnNo.Focus();
                    else if (defaultResult == MessageBoxResult.Cancel)
                        msgBox.btnCancel.Focus();
                    else
                        msgBox.btnYes.Focus();
                    break;
                default:
                    break;
            }
        }

        private static void SetImageOfMessageBox(MessageBoxImage icon)
        {
            switch (icon)
            {
                case MessageBoxImage.Exclamation:
                    msgBox.SetImage("Exclamation.png");
                    msgBox.Img.Visibility = Visibility.Visible;
                    break;
                case MessageBoxImage.Question:
                    msgBox.SetImage("Question.png");
                    msgBox.Img.Visibility = Visibility.Visible;
                    break;
                case MessageBoxImage.Information:
                    msgBox.SetImage("Information.png");
                    msgBox.Img.Visibility = Visibility.Visible;
                    break;
                case MessageBoxImage.Error:
                    msgBox.SetImage("Error.png");
                    msgBox.Img.Visibility = Visibility.Visible;
                    break;
                default:
                    msgBox.Img.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void SetImage(string imageName)
        {
            string uri = Constant.ProgramDirectory + "/Images/" + imageName;
            var uriSource = new Uri(uri, UriKind.RelativeOrAbsolute);
            Img.Source = new BitmapImage(uriSource);
        }

        private void KeyEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
        {
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
            msgBox.Close();
            msgBox = null;
        }
    }
}
