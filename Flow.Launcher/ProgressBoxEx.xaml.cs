using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher
{
    public partial class ProgressBoxEx : Window
    {
        private readonly Action _forceClosed;

        private ProgressBoxEx(Action forceClosed)
        {
            _forceClosed = forceClosed;
            InitializeComponent();
        }

        public static async Task ShowAsync(string caption, Func<Action<double>, Task> reportProgressAsync, Action forceClosed = null)
        {
            ProgressBoxEx prgBox = null;
            try
            {
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        prgBox = new ProgressBoxEx(forceClosed)
                        {
                            Title = caption
                        };
                        prgBox.TitleTextBlock.Text = caption;
                        prgBox.Show();
                    });
                }

                await reportProgressAsync(prgBox.ReportProgress).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log.Error($"|ProgressBoxEx.Show|An error occurred: {e.Message}");

                await reportProgressAsync(null).ConfigureAwait(false);
            }
            finally
            {
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        if (prgBox != null)
                        {
                            await prgBox.CloseAsync();
                        }
                    });
                }
            }
        }

        private void ReportProgress(double progress)
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

        private async Task CloseAsync()
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                await Application.Current.Dispatcher.InvokeAsync(Close);
                return;
            }

            Close();
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
            Close();
            _forceClosed?.Invoke();
        }
    }
}
