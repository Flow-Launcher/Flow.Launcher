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

        public enum MessageBoxType
        {
            ConfirmationWithYesNo = 0,
            ConfirmationWithYesNoCancel,
            YesNo,
            Information,
            Error,
            Warning
        }

        public enum MessageBoxImage
        {
            Warning = 0,
            Question,
            Information,
            Error,
            None
        }

        static MessageBoxEx msgBox;
        static MessageBoxResult _result = MessageBoxResult.No;


        /// 1 parameter
        public static MessageBoxResult Show(string messageBoxText)
        {
            return Show(messageBoxText, string.Empty, MessageBoxButton.OK, MessageBoxImage.None);
        }

        // 2 parameter
        public static MessageBoxResult Show(string messageBoxText, string title)
        {
            return Show(messageBoxText, title, MessageBoxButton.OK, MessageBoxImage.None);
        }

        /// 3 parameter
        public static MessageBoxResult Show(string messageBoxText, string title, MessageBoxType type) 
        {
            switch (type)
            {
                case MessageBoxType.ConfirmationWithYesNo:
                    return Show(messageBoxText, title, MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                case MessageBoxType.YesNo:
                    return Show(messageBoxText, title, MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                case MessageBoxType.ConfirmationWithYesNoCancel:
                    return Show(messageBoxText, title, MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);
                case MessageBoxType.Information:
                    return Show(messageBoxText, title, MessageBoxButton.OK,
                    MessageBoxImage.Information);
                case MessageBoxType.Error:
                    return Show(messageBoxText, title, MessageBoxButton.OK,
                    MessageBoxImage.Error);
                case MessageBoxType.Warning:
                    return Show(messageBoxText, title, MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                default:
                    return MessageBoxResult.No;
            }
        }

        // 4 parameter, Final Display Message. 
        public static MessageBoxResult Show(string messageBoxText, string title, MessageBoxButton button, MessageBoxImage image)
        {
            msgBox = new MessageBoxEx();
            if (title == string.Empty && button == MessageBoxButton.OK && image == MessageBoxImage.None)
            {
                msgBox.DescOnlyTextBlock.Text = messageBoxText;
                msgBox.Title = messageBoxText;
            }
            else
            {
                msgBox.TitleTextBlock.Text = title;
                msgBox.DescTextBlock.Text = messageBoxText;
                msgBox.Title = title;
                SetImageOfMessageBox(image);
            }
            SetVisibilityOfButtons(button);
            msgBox.ShowDialog();
            return _result;
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

        private static void SetVisibilityOfButtons(MessageBoxButton button)
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
                    msgBox.btnCancel.Focus();
                    break;
                case MessageBoxButton.YesNo:
                    msgBox.btnOk.Visibility = Visibility.Collapsed;
                    msgBox.btnCancel.Visibility = Visibility.Collapsed;
                    msgBox.btnNo.Focus();
                    break;
                case MessageBoxButton.YesNoCancel:
                    msgBox.btnOk.Visibility = Visibility.Collapsed;
                    msgBox.btnCancel.Focus();
                    break;
                default:
                    break;
            }
        }
        private static void SetImageOfMessageBox(MessageBoxImage image)
        {
            switch (image)
            {
                case MessageBoxImage.Warning:
                    msgBox.SetImage("Warning.png");
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
        private void cmdEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Button_Cancel(object sender, RoutedEventArgs e)
        {
            msgBox.Close();
            msgBox = null;
        }

    }
}
