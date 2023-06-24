using Flow.Launcher.Core.ExternalPlugins;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
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
            string path = Log.CurrentLogDirectory;
            var directory = new DirectoryInfo(path);
            var log = directory.GetFiles().OrderByDescending(f => f.LastWriteTime).First();

            var websiteUrl = exception switch
                {
                    FlowPluginException pluginException =>GetIssuesUrl(pluginException.Metadata.Website),
                    _ => Constant.IssuesUrl
                };
                

            var paragraph = Hyperlink("Please open new issue in: ", websiteUrl);
            paragraph.Inlines.Add($"1. upload log file: {log.FullName}\n");
            paragraph.Inlines.Add($"2. copy below exception message");
            ErrorTextbox.Document.Blocks.Add(paragraph);

            StringBuilder content = new StringBuilder();
            content.AppendLine(ErrorReporting.RuntimeInfo());
            content.AppendLine(ErrorReporting.DependenciesInfo());
            content.AppendLine($"Date: {DateTime.Now.ToString(CultureInfo.InvariantCulture)}");
            content.AppendLine("Exception:");
            content.AppendLine(exception.ToString());
            paragraph = new Paragraph();
            paragraph.Inlines.Add(content.ToString());
            ErrorTextbox.Document.Blocks.Add(paragraph);
        }

        private Paragraph Hyperlink(string textBeforeUrl, string url)
        {
            var paragraph = new Paragraph();
            paragraph.Margin = new Thickness(0);

            var link = new Hyperlink
            {
                IsEnabled = true
            };
            link.Inlines.Add(url);
            link.NavigateUri = new Uri(url);
            link.Click += (s, e) => SearchWeb.OpenInBrowserTab(url);

            paragraph.Inlines.Add(textBeforeUrl);
            paragraph.Inlines.Add(link);
            paragraph.Inlines.Add("\n");

            return paragraph;
        }
    }
}
