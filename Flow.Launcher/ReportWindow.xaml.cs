using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using Flow.Launcher.Core.ExternalPlugins;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher
{
    internal partial class ReportWindow
    {
        public ReportWindow(Exception exception)
        {
            InitializeComponent();
            ErrorTextbox.Document.Blocks.FirstBlock.Margin = new Thickness(0);
            SetException(exception);
        }

        private static string GetIssuesUrl(string website)
        {
            if (!website.StartsWith("https://github.com"))
            {
                return website;
            }
            if(website.Contains("Flow-Launcher/Flow.Launcher"))
            {
                return Constant.IssuesUrl;
            }
            var treeIndex = website.IndexOf("tree", StringComparison.Ordinal);
            return treeIndex == -1 ? $"{website}/issues" : $"{website[..treeIndex]}/issues";
        }

        private void SetException(Exception exception)
        {
            var path = DataLocation.VersionLogDirectory;
            var directory = new DirectoryInfo(path);
            var log = directory.GetFiles().OrderByDescending(f => f.LastWriteTime).First();

            var websiteUrl = exception switch
            {
                FlowPluginException pluginException => GetIssuesUrl(pluginException.Metadata.Website),
                _ => Constant.IssuesUrl
            };

            var paragraph = Hyperlink(App.API.GetTranslation("reportWindow_please_open_issue"), websiteUrl);
            paragraph.Inlines.Add(string.Format(App.API.GetTranslation("reportWindow_upload_log"), log.FullName));
            paragraph.Inlines.Add("\n");
            paragraph.Inlines.Add(App.API.GetTranslation("reportWindow_copy_below"));
            ErrorTextbox.Document.Blocks.Add(paragraph);

            StringBuilder content = new StringBuilder();
            content.AppendLine(ErrorReporting.RuntimeInfo());
            content.AppendLine(ErrorReporting.DependenciesInfo());
            content.AppendLine();
            content.AppendLine($"Date: {DateTime.Now.ToString(CultureInfo.InvariantCulture)}");
            content.AppendLine("Exception:");
            content.AppendLine(exception.ToString());
            paragraph = new Paragraph();
            paragraph.Inlines.Add(content.ToString());
            ErrorTextbox.Document.Blocks.Add(paragraph);
        }

        private static Paragraph Hyperlink(string textBeforeUrl, string url)
        {
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0)
            };

            Hyperlink link = null;
            try
            {
                var uri = new Uri(url);

                link = new Hyperlink
                {
                    IsEnabled = true
                };
                link.Inlines.Add(url);
                link.NavigateUri = uri;
                link.Click += (s, e) => SearchWeb.OpenInBrowserTab(url);
            }
            catch (Exception)
            {
                // Leave link as null if the URL is invalid
            }

            paragraph.Inlines.Add(textBeforeUrl);
            paragraph.Inlines.Add(" ");
            if (link is null)
            {
                // Add the URL as plain text if it is invalid
                paragraph.Inlines.Add(url);
            }
            else
            {
                // Add the hyperlink if it is valid
                paragraph.Inlines.Add(link);
            }
            paragraph.Inlines.Add("\n");

            return paragraph;
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
