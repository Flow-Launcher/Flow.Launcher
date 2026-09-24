using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Flow.Launcher.Infrastructure;

namespace Flow.Launcher
{
    public partial class MessageBoxEx : Window
    {
        private static readonly string ClassName = nameof(MessageBoxEx);

        private static MessageBoxEx msgBox;
        private static MessageBoxResult _result = MessageBoxResult.None;

        private readonly MessageBoxButton _button;

        private MessageBoxEx(MessageBoxButton button)
        {
            _button = button;
            InitializeComponent();

            // For YesNo dialogs, hide the close button to match native windows message box behavior
            // https://learn.microsoft.com/en-us/dotnet/api/system.windows.messageboxbutton?view=windowsdesktop-10.0#remarks
            if (_button == MessageBoxButton.YesNo)
                TitleBar.ShowCloseButton = false;
        }

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
                if (caption == string.Empty && icon == MessageBoxImage.None)
                {
                    // If there is no caption and no icon, use DescOnlyTextBlock for vertically centered text
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

                // This ensures that if the message box is closed without using the answer buttons
                // It will still have a meaningful result based on its type
                _result = msgBox.DefaultCloseResult;

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
            var imageSource = await App.API.LoadImageAsync(imagePath);
            Img.Source = imageSource;
        }

        // What the result should be if the message box is closed outside of the direct response buttons - e.g title bar close button
        // Mostly replicates System.Windows.MessageBox behavior
        // https://learn.microsoft.com/en-us/dotnet/api/system.windows.messageboxresult#remarks
        private MessageBoxResult DefaultCloseResult => _button switch
        {
            MessageBoxButton.OK => MessageBoxResult.OK,
            MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
            MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
            
            // For YesNo this should only be used in a forced close edge case (e.g. alt f4)
            // Most callers make the mistake of checking for No instead of not Yes - so best not to return None etc
            MessageBoxButton.YesNo => MessageBoxResult.No,
            
            // covers unsupported types, e.g. AbortRetryIgnore
            _ => MessageBoxResult.None 
        };


        private void KeyEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
        {
            if (_button == MessageBoxButton.YesNo)
                // Follow System.Windows.MessageBox behavior
                return;
            
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
            e.Handled = true;

            if (_button == MessageBoxButton.YesNo)
            {
                // For YesNo, the close button should be hidden and inaccessible.
                App.API.LogWarn(ClassName, "Close button was invoked despite being hidden for YesNo dialog");
                return;
            }

            msgBox.Close();
            msgBox = null;
        }
    }
}
