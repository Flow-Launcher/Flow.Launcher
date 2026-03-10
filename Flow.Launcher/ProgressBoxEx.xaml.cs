using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Flow.Launcher.Core.Resource;

namespace Flow.Launcher
{
    public partial class ProgressBoxEx : Window
    {
        private static readonly string ClassName = nameof(ProgressBoxEx);

        private readonly Action _cancelProgress;

        private ProgressBoxEx(Action cancelProgress)
        {
            _cancelProgress = cancelProgress;
            InitializeComponent();
        }

        public static async Task ShowAsync(string caption, Func<Action<double>, Task> reportProgressAsync, Action cancelProgress = null)
        {
            ProgressBoxEx progressBox = null;
            try
            {
                await DispatcherHelper.InvokeAsync(() =>
                {
                    progressBox = new ProgressBoxEx(cancelProgress)
                    {
                        Title = caption
                    };
                    progressBox.TitleTextBlock.Text = caption;
                    progressBox.Show();
                });

                await reportProgressAsync(progressBox.ReportProgress).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                App.API.LogError(ClassName, $"An error occurred: {e.Message}");

                await reportProgressAsync(null).ConfigureAwait(false);
            }
            finally
            {
                await DispatcherHelper.InvokeAsync(() =>
                {
                    progressBox?.Close();
                });
            }
        }

        private void ReportProgress(double progress)
        {
            DispatcherHelper.Invoke(() => ReportProgress1(progress));
        }

        private void ReportProgress1(double progress)
        {
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

        private void KeyEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
        {
            ForceClose();
        }

        private void Button_Cancel(object sender, RoutedEventArgs e)
        {
            ForceClose();
        }

        private void Button_Minimize(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Button_Background(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void ForceClose()
        {
            Close();
            _cancelProgress?.Invoke();
        }
    }
}
