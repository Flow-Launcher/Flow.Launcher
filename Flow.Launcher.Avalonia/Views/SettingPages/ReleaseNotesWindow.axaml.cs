using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.DependencyInjection;
using FluentAvalonia.UI.Controls;
using Flow.Launcher.Avalonia.Resource;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Http;

namespace Flow.Launcher.Avalonia.Views.SettingPages;

public partial class ReleaseNotesWindow : Window, INotifyPropertyChanged
{
    private static readonly string ReleaseNotesApiUrl = Constant.GitHub.Replace("https://github.com/", "https://api.github.com/repos/") + "/releases";

    private readonly Internationalization _i18n;
    private bool _isLoading;
    private string _releaseNotesText = string.Empty;

    public ReleaseNotesWindow()
    {
        _i18n = Ioc.Default.GetRequiredService<Internationalization>();
        _releaseNotesText = Translate("releaseNotes", "Release notes");

        InitializeComponent();
        DataContext = this;
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    public string ReleaseNotesUrl => Constant.GitHub + "/releases";

    public string ReleaseNotesText
    {
        get => _releaseNotesText;
        private set
        {
            if (_releaseNotesText == value)
            {
                return;
            }

            _releaseNotesText = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value)
            {
                return;
            }

            _isLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotLoading));
        }
    }

    public bool IsNotLoading => !IsLoading;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _ = LoadReleaseNotesAsync();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnOpenWebsiteClick(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(ReleaseNotesUrl) { UseShellExecute = true });
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        await LoadReleaseNotesAsync();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async System.Threading.Tasks.Task LoadReleaseNotesAsync()
    {
        IsLoading = true;

        try
        {
            var markdown = await GetReleaseNotesMarkdownAsync();
            if (string.IsNullOrWhiteSpace(markdown))
            {
                ReleaseNotesText = Translate("checkNetworkConnectionSubTitle", "Please check your network connection and try again.");
                await ShowMessageAsync(
                    Translate("checkNetworkConnectionTitle", "Unable to load release notes"),
                    Translate("checkNetworkConnectionSubTitle", "Please check your network connection and try again."));
                return;
            }

            ReleaseNotesText = markdown;
        }
        catch (Exception e)
        {
            ReleaseNotesText = e.Message;
            await ShowMessageAsync(
                Translate("checkNetworkConnectionTitle", "Unable to load release notes"),
                e.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async System.Threading.Tasks.Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = Translate("commonOK", "OK")
        };

        await dialog.ShowAsync(this);
    }

    private static async System.Threading.Tasks.Task<string> GetReleaseNotesMarkdownAsync()
    {
        var releaseNotesJson = await Http.GetStringAsync(ReleaseNotesApiUrl);
        if (string.IsNullOrWhiteSpace(releaseNotesJson))
        {
            return string.Empty;
        }

        var releases = JsonSerializer.Deserialize<List<GitHubReleaseInfo>>(releaseNotesJson);
        if (releases is null || releases.Count == 0)
        {
            return string.Empty;
        }

        var latestReleases = releases.OrderByDescending(release => release.PublishedDate).Take(3).ToList();
        var builder = new StringBuilder();

        for (var i = 0; i < latestReleases.Count; i++)
        {
            var release = latestReleases[i];
            builder.AppendLine($"# {release.Name}");
            builder.AppendLine(release.ReleaseNotes ?? string.Empty);

            if (i < latestReleases.Count - 1)
            {
                builder.AppendLine();
                builder.AppendLine("---");
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private string Translate(string key, string fallback)
    {
        var value = _i18n.GetTranslation(key);
        return value.StartsWith('[') ? fallback : value;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class GitHubReleaseInfo
    {
        [JsonPropertyName("published_at")]
        public DateTimeOffset PublishedDate { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string? ReleaseNotes { get; set; }
    }
}
