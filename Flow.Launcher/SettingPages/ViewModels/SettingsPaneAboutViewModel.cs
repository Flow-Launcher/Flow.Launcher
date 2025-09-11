using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneAboutViewModel : BaseModel
{
    private static readonly string ClassName = nameof(SettingsPaneAboutViewModel);

    private readonly Settings _settings;
    private readonly Updater _updater;

    public string LogFolderSize
    {
        get
        {
            var size = GetLogFiles().Sum(file => file.Length);
            return $"{App.API.GetTranslation("clearlogfolder")} ({BytesToReadableString(size)})";
        }
    }

    public string CacheFolderSize
    {
        get
        {
            var size = GetCacheFiles().Sum(file => file.Length);
            return $"{App.API.GetTranslation("clearcachefolder")} ({BytesToReadableString(size)})";
        }
    }

    public string Website => Constant.Website;
    public string SponsorPage => Constant.SponsorPage;
    public string ReleaseNotes => _updater.GitHubRepository + "/releases/latest";
    public string Documentation => Constant.Documentation;
    public string Docs => Constant.Docs;
    public string Github => Constant.GitHub;

    public string Version => Constant.Version switch
    {
        "1.0.0" => Constant.Dev,
        _ => Constant.Version
    };

    public string ActivatedTimes => string.Format(
        App.API.GetTranslation("about_activate_times"),
        _settings.ActivateTimes
    );

    public class LogLevelData : DropdownDataGeneric<LOGLEVEL> { }

    public List<LogLevelData> LogLevels { get; } =
        DropdownDataGeneric<LOGLEVEL>.GetValues<LogLevelData>("LogLevel");

    public LOGLEVEL LogLevel
    {
        get => _settings.LogLevel;
        set
        {
            if (_settings.LogLevel != value)
            {
                _settings.LogLevel = value;

                Log.SetLogLevel(value);
            }
        }
    }

    public SettingsPaneAboutViewModel(Settings settings, Updater updater)
    {
        _settings = settings;
        _updater = updater;
        UpdateEnumDropdownLocalizations();
    }

    private void UpdateEnumDropdownLocalizations()
    {
        DropdownDataGeneric<LOGLEVEL>.UpdateLabels(LogLevels);
    }

    [RelayCommand]
    private void OpenWelcomeWindow()
    {
        var window = new WelcomeWindow();
        window.ShowDialog();
    }

    [RelayCommand]
    private void AskClearLogFolderConfirmation()
    {
        var confirmResult = App.API.ShowMsgBox(
            App.API.GetTranslation("clearlogfolderMessage"),
            App.API.GetTranslation("clearlogfolder"),
            MessageBoxButton.YesNo
        );

        if (confirmResult == MessageBoxResult.Yes)
        {
            if (!ClearLogFolder())
            {
                App.API.ShowMsgBox(App.API.GetTranslation("clearfolderfailMessage"));
            }
        }
    }

    [RelayCommand]
    private void AskClearCacheFolderConfirmation()
    {
        var confirmResult = App.API.ShowMsgBox(
            App.API.GetTranslation("clearcachefolderMessage"),
            App.API.GetTranslation("clearcachefolder"),
            MessageBoxButton.YesNo
        );

        if (confirmResult == MessageBoxResult.Yes)
        {
            if (!ClearCacheFolder())
            {
                App.API.ShowMsgBox(App.API.GetTranslation("clearfolderfailMessage"));
            }
        }
    }

    [RelayCommand]
    private void OpenSettingsFolder()
    {
        App.API.OpenDirectory(DataLocation.SettingsDirectory);
    }

    [RelayCommand]
    private void OpenParentOfSettingsFolder(object parameter)
    {
        string settingsFolderPath = Path.Combine(DataLocation.SettingsDirectory);
        string parentFolderPath = Path.GetDirectoryName(settingsFolderPath);
        App.API.OpenDirectory(parentFolderPath);
    }

    [RelayCommand]
    private void OpenCacheFolder()
    {
        App.API.OpenDirectory(DataLocation.CacheDirectory);
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        App.API.OpenDirectory(GetLogDir(Constant.Version).FullName);
    }

    [RelayCommand]
    private Task UpdateAppAsync() => _updater.UpdateAppAsync(false);

    private bool ClearLogFolder()
    {
        var success = true;
        var logDirectory = GetLogDir();
        var logFiles = GetLogFiles();

        logFiles.ForEach(f =>
        {
            try
            {
                f.Delete();
            }
            catch (Exception e)
            {
                App.API.LogException(ClassName, $"Failed to delete log file: {f.Name}", e);
                success = false;
            }
        });

        logDirectory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
            // Do not clean log files of current version
            .Where(dir => !Constant.Version.Equals(dir.Name))
            .ToList()
            .ForEach(dir =>
            {
                try
                {
                    // Log folders are the last level of folders
                    dir.Delete(recursive: false);
                }
                catch (Exception e)
                {
                    App.API.LogException(ClassName, $"Failed to delete log directory: {dir.Name}", e);
                    success = false;
                }
            });

        OnPropertyChanged(nameof(LogFolderSize));

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

    private bool ClearCacheFolder()
    {
        var success = true;
        var cacheDirectory = GetCacheDir();
        var pluginCacheDirectory = GetPluginCacheDir();
        var cacheFiles = GetCacheFiles();

        cacheFiles.ForEach(f =>
        {
            try
            {
                f.Delete();
            }
            catch (Exception e)
            {
                App.API.LogException(ClassName, $"Failed to delete cache file: {f.Name}", e);
                success = false;
            }
        });

        // Firstly, delete plugin cache directories
        pluginCacheDirectory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
            .ToList()
            .ForEach(dir =>
            {
                try
                {
                    // Plugin may create directories in its cache directory
                    dir.Delete(recursive: true);
                }
                catch (Exception e)
                {
                    App.API.LogException(ClassName, $"Failed to delete cache directory: {dir.Name}", e);
                    success = false;
                }
            });

        // Then, delete plugin directory
        var dir = GetPluginCacheDir();
        try
        {
            dir.Delete(recursive: false);
        }
        catch (Exception e)
        {
            App.API.LogException(ClassName, $"Failed to delete cache directory: {dir.Name}", e);
            success = false;
        }

        OnPropertyChanged(nameof(CacheFolderSize));

        return success;
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
        string[] orders = { "GB", "MB", "KB", "B" };
        long max = (long)Math.Pow(scale, orders.Length - 1);

        foreach (string order in orders)
        {
            if (bytes > max) return $"{decimal.Divide(bytes, max):##.##} {order}";

            max /= scale;
        }

        return "0 B";
    }
    
    public string SettingWindowFont
    {
        get => _settings.SettingWindowFont;
        set
        {
            if (_settings.SettingWindowFont != value)
            {
                _settings.SettingWindowFont = value;
                OnPropertyChanged();
            }
        }
    }

    [RelayCommand]
    private void ResetSettingWindowFont()
    {
        SettingWindowFont = Win32Helper.GetSystemDefaultFont(false);
    }

    [RelayCommand]
    private void OpenReleaseNotes()
    {
        var releaseNotesWindow = new ReleaseNotesWindow();
        releaseNotesWindow.Show();
    }

    [RelayCommand]
    private void OpenSponsorPage()
    {
        App.API.OpenUrl(SponsorPage);
    }
}
