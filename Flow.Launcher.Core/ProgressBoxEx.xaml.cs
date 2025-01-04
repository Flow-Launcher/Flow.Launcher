using System;
using System.Windows;
using System.Windows.Input;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Core
{
    public partial class ProgressBoxEx : Window
    {
        private ProgressBoxEx()
        {
            InitializeComponent();
        }

        public static ProgressBoxEx Show(string caption)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                return Application.Current.Dispatcher.Invoke(() => Show(caption));
            }

            try
            {
                var prgBox = new ProgressBoxEx
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
            }
            else
            {
                ProgressBar.Value = progress;
            }
        }

        private void KeyEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Button_Cancel(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
