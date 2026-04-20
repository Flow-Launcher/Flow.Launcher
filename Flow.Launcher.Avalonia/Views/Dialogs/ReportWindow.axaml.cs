using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Flow.Launcher.Core.ExternalPlugins;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Exception;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Avalonia.Views.Dialogs;

public partial class ReportWindow : Window, INotifyPropertyChanged
{
    public ReportWindow(Exception exception)
    {
        IssueUrl = GetIssueUrl(exception);
        ExceptionReportText = BuildReportText(exception);

        InitializeComponent();
        DataContext = this;
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    public string IssueUrl { get; }

    public string ExceptionReportText { get; }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (Clipboard != null)
        {
            await Clipboard.SetTextAsync(ExceptionReportText);
        }
    }

    private void OnOpenIssueClick(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(IssueUrl) { UseShellExecute = true });
    }

    private void OnOpenLogsClick(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(DataLocation.VersionLogDirectory) { UseShellExecute = true });
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private static string GetIssueUrl(Exception exception)
    {
        if (exception is not FlowPluginException pluginException)
        {
            return Constant.IssuesUrl;
        }

        var website = pluginException.Metadata.Website;
        if (!website.StartsWith("https://github.com", StringComparison.OrdinalIgnoreCase))
        {
            return website;
        }

        if (website.Contains("Flow-Launcher/Flow.Launcher", StringComparison.OrdinalIgnoreCase))
        {
            return Constant.IssuesUrl;
        }

        var treeIndex = website.IndexOf("tree", StringComparison.Ordinal);
        return treeIndex == -1 ? $"{website}/issues" : $"{website[..treeIndex]}/issues";
    }

    private static string BuildReportText(Exception exception)
    {
        var exceptionReport = ExceptionFormatter.FormatExcpetion(exception);
        var latestLog = TryGetLatestLogFile();

        if (string.IsNullOrWhiteSpace(latestLog))
        {
            return exceptionReport;
        }

        return $"Latest log: {latestLog}{Environment.NewLine}{Environment.NewLine}{exceptionReport}";
    }

    private static string? TryGetLatestLogFile()
    {
        try
        {
            var directory = new DirectoryInfo(DataLocation.VersionLogDirectory);
            if (!directory.Exists)
            {
                return null;
            }

            return directory.GetFiles().OrderByDescending(file => file.LastWriteTime).FirstOrDefault()?.FullName;
        }
        catch
        {
            return null;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
