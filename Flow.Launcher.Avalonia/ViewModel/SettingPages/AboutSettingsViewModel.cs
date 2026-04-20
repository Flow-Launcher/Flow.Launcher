using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Avalonia.Resource;
using Flow.Launcher.Avalonia.Views.Dialogs;
using Flow.Launcher.Avalonia.Views.SettingPages;
using Flow.Launcher.Core;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class AboutSettingsViewModel : ObservableObject
{
    private static readonly string ClassName = nameof(AboutSettingsViewModel);

    private readonly Settings _settings;
    private readonly Updater _updater;
    private readonly Internationalization _i18n;

    public AboutSettingsViewModel()
    {
        _settings = Ioc.Default.GetRequiredService<Settings>();
        _updater = Ioc.Default.GetRequiredService<Updater>();
        _i18n = Ioc.Default.GetRequiredService<Internationalization>();

        LogLevels = DropdownDataGeneric.GetEnumData<LOGLEVEL>("LogLevel");
        AvailableFonts = Fonts.SystemFontFamilies.Select(x => x.Source).OrderBy(x => x).ToList();
    }

    public string Version => Constant.Version switch
    {
        "1.0.0" => Constant.Dev,
        _ => Constant.Version
    };

    public string Website => Constant.Website;
    public string SponsorPage => Constant.SponsorPage;
    public string ReleaseNotesUrl => Constant.GitHub + "/releases/latest";
    public string Documentation => Constant.Documentation;
    public string Docs => Constant.Docs;
    public string GitHub => Constant.GitHub;
    public string Crowdin => Constant.CrowdinProjectUrl;
    public string ActivatedTimes => string.Format(Translate("about_activate_times", "You have activated Flow Launcher {0} times"), _settings.ActivateTimes);

    public string LogFolderSize => $"{Translate("clearlogfolder", "Clear Logs")} ({BytesToReadableString(GetLogFiles().Sum(file => file.Length))})";

    public string CacheFolderSize => $"{Translate("clearcachefolder", "Clear Caches")} ({BytesToReadableString(GetCacheFiles().Sum(file => file.Length))})";

    public List<DropdownDataGeneric<LOGLEVEL>> LogLevels { get; }

    public List<string> AvailableFonts { get; }

    public DropdownDataGeneric<LOGLEVEL>? SelectedLogLevelItem
    {
        get => LogLevels.FirstOrDefault(x => x.Value.Equals(_settings.LogLevel));
        set
        {
            if (value == null || _settings.LogLevel == value.Value)
            {
                return;
            }

            _settings.LogLevel = value.Value;
            Log.SetLogLevel(value.Value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(LogLevel));
        }
    }

    public LOGLEVEL LogLevel
    {
        get => _settings.LogLevel;
        set
        {
            if (_settings.LogLevel == value)
            {
                return;
            }

            _settings.LogLevel = value;
            Log.SetLogLevel(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedLogLevelItem));
        }
    }

    public string SettingWindowFont
    {
        get => _settings.SettingWindowFont;
        set
        {
            if (_settings.SettingWindowFont == value)
            {
                return;
            }

            _settings.SettingWindowFont = value;
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private async Task OpenWebsite()
    {
        Process.Start(new ProcessStartInfo(Website) { UseShellExecute = true });
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenGitHub()
    {
        Process.Start(new ProcessStartInfo(GitHub) { UseShellExecute = true });
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenDocumentation()
    {
        Process.Start(new ProcessStartInfo(Documentation) { UseShellExecute = true });
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenCrowdin()
    {
        Process.Start(new ProcessStartInfo(Crowdin) { UseShellExecute = true });
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenIconsWebsite()
    {
        Process.Start(new ProcessStartInfo("https://icons8.com/") { UseShellExecute = true });
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenSponsorPage()
    {
        Process.Start(new ProcessStartInfo(SponsorPage) { UseShellExecute = true });
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenReleaseNotes()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is null)
        {
            return;
        }

        var window = new ReleaseNotesWindow();
        window.Show(desktop.MainWindow);
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenWelcome()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is null)
        {
            return;
        }

        var window = new WelcomeWindow();
        await window.ShowDialog(desktop.MainWindow);
    }

    [RelayCommand]
    private async Task UpdateApp()
    {
        await _updater.UpdateAppAsync(false);
    }

    [RelayCommand]
    private void OpenSettingsFolder()
    {
        App.API?.OpenDirectory(DataLocation.SettingsDirectory);
    }

    [RelayCommand]
    private void OpenParentOfSettingsFolder()
    {
        var settingsFolderPath = Path.Combine(DataLocation.SettingsDirectory);
        var parentFolderPath = Path.GetDirectoryName(settingsFolderPath);
        if (!string.IsNullOrWhiteSpace(parentFolderPath))
        {
            App.API?.OpenDirectory(parentFolderPath);
        }
    }

    [RelayCommand]
    private void OpenCacheFolder()
    {
        App.API?.OpenDirectory(DataLocation.CacheDirectory);
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        App.API?.OpenDirectory(GetLogDir(Constant.Version).FullName);
    }

    [RelayCommand]
    private void AskClearLogFolderConfirmation()
    {
        var confirmResult = App.API?.ShowMsgBox(
            Translate("clearlogfolderMessage", "Are you sure you want to delete all logs?"),
            Translate("clearlogfolder", "Clear Logs"),
            MessageBoxButton.YesNo) ?? MessageBoxResult.No;

        if (confirmResult == MessageBoxResult.Yes && !ClearLogFolder())
        {
            App.API?.ShowMsgBox(Translate("clearfolderfailMessage", "Failed to clear part of folders and files. Please see log file for more information"));
        }
    }

    [RelayCommand]
    private void AskClearCacheFolderConfirmation()
    {
        var confirmResult = App.API?.ShowMsgBox(
            Translate("clearcachefolderMessage", "Are you sure you want to delete all caches?"),
            Translate("clearcachefolder", "Clear Caches"),
            MessageBoxButton.YesNo) ?? MessageBoxResult.No;

        if (confirmResult == MessageBoxResult.Yes && !ClearCacheFolder())
        {
            App.API?.ShowMsgBox(Translate("clearfolderfailMessage", "Failed to clear part of folders and files. Please see log file for more information"));
        }
    }

    [RelayCommand]
    private void ResetSettingWindowFont()
    {
        SettingWindowFont = Win32Helper.GetSystemDefaultFont(false);
    }

    private bool ClearLogFolder()
    {
        var success = true;
        var logDirectory = GetLogDir();
        var logFiles = GetLogFiles();

        logFiles.ForEach(file =>
        {
            try
            {
                file.Delete();
            }
            catch (Exception e)
            {
                App.API?.LogException(ClassName, $"Failed to delete log file: {file.Name}", e);
                success = false;
            }
        });

        logDirectory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
            .Where(dir => !Constant.Version.Equals(dir.Name, StringComparison.Ordinal))
            .ToList()
            .ForEach(dir =>
            {
                try
                {
                    dir.Delete(recursive: false);
                }
                catch (Exception e)
                {
                    App.API?.LogException(ClassName, $"Failed to delete log directory: {dir.Name}", e);
                    success = false;
                }
            });

        OnPropertyChanged(nameof(LogFolderSize));
        return success;
    }

    private bool ClearCacheFolder()
    {
        var success = true;
        var pluginCacheDirectory = GetPluginCacheDir();
        var cacheFiles = GetCacheFiles();

        cacheFiles.ForEach(file =>
        {
            try
            {
                file.Delete();
            }
            catch (Exception e)
            {
                App.API?.LogException(ClassName, $"Failed to delete cache file: {file.Name}", e);
                success = false;
            }
        });

        if (pluginCacheDirectory.Exists)
        {
            pluginCacheDirectory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
                .ToList()
                .ForEach(dir =>
                {
                    try
                    {
                        dir.Delete(recursive: true);
                    }
                    catch (Exception e)
                    {
                        App.API?.LogException(ClassName, $"Failed to delete cache directory: {dir.Name}", e);
                        success = false;
                    }
                });

            try
            {
                pluginCacheDirectory.Delete(recursive: false);
            }
            catch (Exception e)
            {
                App.API?.LogException(ClassName, $"Failed to delete cache directory: {pluginCacheDirectory.Name}", e);
                success = false;
            }
        }

        OnPropertyChanged(nameof(CacheFolderSize));
        return success;
    }

    private static DirectoryInfo GetLogDir(string version = "")
    {
        return new DirectoryInfo(Path.Combine(DataLocation.LogDirectory, version));
    }

    private static List<FileInfo> GetLogFiles(string version = "")
    {
        return GetLogDir(version).EnumerateFiles("*", SearchOption.AllDirectories).ToList();
    }

    private static DirectoryInfo GetCacheDir()
    {
        return new DirectoryInfo(DataLocation.CacheDirectory);
    }

    private static DirectoryInfo GetPluginCacheDir()
    {
        return new DirectoryInfo(DataLocation.PluginCacheDirectory);
    }

    private static List<FileInfo> GetCacheFiles()
    {
        return GetCacheDir().EnumerateFiles("*", SearchOption.AllDirectories).ToList();
    }

    private static string BytesToReadableString(long bytes)
    {
        const int scale = 1024;
        string[] orders = ["GB", "MB", "KB", "B"];
        long max = (long)Math.Pow(scale, orders.Length - 1);

        foreach (var order in orders)
        {
            if (bytes > max)
            {
                return $"{decimal.Divide(bytes, max):##.##} {order}";
            }

            max /= scale;
        }

        return "0 B";
    }

    private string Translate(string key, string fallback)
    {
        var value = _i18n.GetTranslation(key);
        return value.StartsWith('[') ? fallback : value;
    }
}
