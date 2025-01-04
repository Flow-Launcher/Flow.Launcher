using System;
using System.Windows;
using System.Windows.Input;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core
{
    public partial class ProgressBoxEx : Window, IProgressBoxEx
    {
        private readonly Action _forceClosed;
        private bool _isClosed;

        private ProgressBoxEx(Action forceClosed)
        {
            _forceClosed = forceClosed;
            InitializeComponent();
        }

        public static IProgressBoxEx Show(string caption, Action forceClosed = null)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                return Application.Current.Dispatcher.Invoke(() => Show(caption, forceClosed));
            }

            try
            {
                var prgBox = new ProgressBoxEx(forceClosed)
                {
                    Title = caption
                };
                prgBox.TitleTextBlock.Text = caption;
                prgBox.Show();
                return prgBox;
            }
            catch (Exception e)
            {
                Log.Error($"|ProgressBoxEx.Show|An error occurred: {e.Message}");
                return null;
            }
        }

        public void ReportProgress(double progress)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => ReportProgress(progress));
                return;
            }

            if (progress < 0)
            {
                ProgressBar.Value = 0;
            }
            else if (progress >= 100)
            {
                ProgressBar.Value = 100;
                Close();
            }
            else
            {
                ProgressBar.Value = progress;
            }
        }

        private new void Close()
        {
            if (_isClosed)
            {
                return;
            }
            
            base.Close();
            _isClosed = true;
        }

        private void KeyEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
        {
            ForceClose();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ForceClose();
        }

        private void Button_Cancel(object sender, RoutedEventArgs e)
        {
            ForceClose();
        }

        private void ForceClose()
        {
            if (_isClosed)
            {
                return;
            }
            
            base.Close();
            _isClosed = true;
            _forceClosed?.Invoke();
        }
    }
}
