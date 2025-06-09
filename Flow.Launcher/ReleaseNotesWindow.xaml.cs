using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Flow.Launcher.Infrastructure.Http;

namespace Flow.Launcher
{
    public partial class ReleaseNotesWindow : Window
    {
        private static readonly string ReleaseNotes = Properties.Settings.Default.GithubRepo + "/releases";

        public ReleaseNotesWindow()
        {
            InitializeComponent();
            SeeMore.Uri = ReleaseNotes;
            ModernWpf.ThemeManager.Current.ActualApplicationThemeChanged += ThemeManager_ActualApplicationThemeChanged;
        }

        #region Window Events

        private void ThemeManager_ActualApplicationThemeChanged(ModernWpf.ThemeManager sender, object args)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (ModernWpf.ThemeManager.Current.ActualApplicationTheme == ModernWpf.ApplicationTheme.Light)
                {
                    MarkdownViewer.MarkdownStyle = (Style)Application.Current.Resources["DocumentStyleGithubLikeLight"];
                    MarkdownViewer.Foreground = Brushes.Black;
                    MarkdownViewer.Background = Brushes.White;
                }
                else
                {
                    MarkdownViewer.MarkdownStyle = (Style)Application.Current.Resources["DocumentStyleGithubLikeDark"];
                    MarkdownViewer.Foreground = Brushes.White;
                    MarkdownViewer.Background = Brushes.Black;
                }
            });
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshMaximizeRestoreButton();
            ThemeManager_ActualApplicationThemeChanged(null, null);
        }

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            ModernWpf.ThemeManager.Current.ActualApplicationThemeChanged -= ThemeManager_ActualApplicationThemeChanged;
        }

        #endregion

        #region Window Custom TitleBar

        private void OnMinimizeButtonClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeRestoreButtonClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState switch
            {
                WindowState.Maximized => WindowState.Normal,
                _ => WindowState.Maximized
            };
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RefreshMaximizeRestoreButton()
        {
            if (WindowState == WindowState.Maximized)
            {
                MaximizeButton.Visibility = Visibility.Hidden;
                RestoreButton.Visibility = Visibility.Visible;
            }
            else
            {
                MaximizeButton.Visibility = Visibility.Visible;
                RestoreButton.Visibility = Visibility.Hidden;
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            RefreshMaximizeRestoreButton();
        }

        #endregion

        #region Control Events

        private void MarkdownViewer_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshMarkdownViewer();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshButton.Visibility = Visibility.Collapsed;
            RefreshProgressRing.Visibility = Visibility.Visible;
            RefreshMarkdownViewer();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void RefreshMarkdownViewer()
        {
            var output = await GetReleaseNotesMarkdownAsync().ConfigureAwait(false);

            Application.Current.Dispatcher.Invoke(() =>
            {
                RefreshProgressRing.Visibility = Visibility.Collapsed;
                if (string.IsNullOrEmpty(output))
                {
                    RefreshButton.Visibility = Visibility.Visible;
                    MarkdownViewer.Visibility = Visibility.Collapsed;
                    App.API.ShowMsgError(
                        App.API.GetTranslation("checkNetworkConnectionTitle"),
                        App.API.GetTranslation("checkNetworkConnectionSubTitle"));
                }
                else
                {
                    RefreshButton.Visibility = Visibility.Collapsed;
                    MarkdownViewer.Markdown = output;
                    MarkdownViewer.Visibility = Visibility.Visible;
                }
            });
        }

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            MarkdownScrollViewer.Height = e.NewSize.Height;
            MarkdownScrollViewer.Width = e.NewSize.Width;
        }

        private void MarkdownViewer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            RaiseMouseWheelEvent(sender, e);
        }

        private void MarkdownViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            RaiseMouseWheelEvent(sender, e);
        }

        private void RaiseMouseWheelEvent(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true; // Prevent the inner control from handling the event

            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };

            // Raise the event on the parent ScrollViewer
            MarkdownScrollViewer.RaiseEvent(eventArg);
        }

        #endregion

        #region Release Notes

        private static async Task<string> GetReleaseNotesMarkdownAsync()
        {
            var releaseNotesJSON = await Http.GetStringAsync("https://api.github.com/repos/Flow-Launcher/Flow.Launcher/releases");

            if (string.IsNullOrEmpty(releaseNotesJSON))
            {
                return string.Empty;
            }
            var releases = JsonSerializer.Deserialize<List<GitHubReleaseInfo>>(releaseNotesJSON);

            // Get the latest releases
            var latestReleases = releases.OrderByDescending(release => release.PublishedDate).Take(3);

            // Build the release notes in Markdown format
            var releaseNotesHtmlBuilder = new StringBuilder(string.Empty);
            foreach (var release in latestReleases)
            {
                releaseNotesHtmlBuilder.AppendLine("# " + release.Name);

                // Because MdXaml.Html package cannot correctly render images without units,
                // We need to manually add unit for images
                // E.g. Replace <img src="..." width="500"> with <img src="..." width="500px">
                var notes = ImageUnitRegex().Replace(release.ReleaseNotes, m =>
                    {
                        var prefix = m.Groups[1].Value;
                        var widthValue = m.Groups[2].Value;
                        var quote = m.Groups[3].Value;
                        var suffix = m.Groups[4].Value;
                        // Only replace if width is number like 500 without units like 500px
                        if (IsNumber(widthValue))
                            return $"{prefix}{widthValue}px{quote}{suffix}";
                        return m.Value;
                    });

                releaseNotesHtmlBuilder.AppendLine(notes);
                releaseNotesHtmlBuilder.AppendLine();
            }

            return releaseNotesHtmlBuilder.ToString();
        }

        private static bool IsNumber(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            foreach (char c in input)
            {
                if (!char.IsDigit(c))
                    return false;
            }
            return true;
        }

        private sealed class GitHubReleaseInfo
        {
            [JsonPropertyName("published_at")]
            public DateTimeOffset PublishedDate { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("tag_name")]
            public string TagName { get; set; }

            [JsonPropertyName("body")]
            public string ReleaseNotes { get; set; }
        }

        [GeneratedRegex("(<img\\s+[^>]*width\\s*=\\s*[\"']?)(\\d+)([\"']?)([^>]*>)", RegexOptions.IgnoreCase, "en-GB")]
        private static partial Regex ImageUnitRegex();

        #endregion
    }
}
