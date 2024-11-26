using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneAboutViewModel : BaseModel
{
    private readonly Settings _settings;
    private readonly Updater _updater;

    public string LogFolderSize
    {
        get
        {
            var size = GetLogFiles().Sum(file => file.Length);
            return $"{InternationalizationManager.Instance.GetTranslation("clearlogfolder")} ({BytesToReadableString(size)})";
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
        InternationalizationManager.Instance.GetTranslation("about_activate_times"),
        _settings.ActivateTimes
    );

    public SettingsPaneAboutViewModel(Settings settings, Updater updater)
    {
        _settings = settings;
        _updater = updater;
    }

    [RelayCommand]
    private void OpenWelcomeWindow()
    {
        var window = new WelcomeWindow(_settings);
        window.ShowDialog();
    }

    [RelayCommand]
    private void AskClearLogFolderConfirmation()
    {
        var confirmResult = MessageBoxEx.Show(
            InternationalizationManager.Instance.GetTranslation("clearlogfolderMessage"),
            InternationalizationManager.Instance.GetTranslation("clearlogfolder"),
            MessageBoxButton.YesNo
        );

        if (confirmResult == MessageBoxResult.Yes)
        {
            ClearLogFolder();
        }
    }

    [RelayCommand]
    private void OpenSettingsFolder()
    {
        PluginManager.API.OpenDirectory(Path.Combine(DataLocation.DataDirectory(), Constant.Settings));
    }

    [RelayCommand]
    private void OpenParentOfSettingsFolder(object parameter)
    {
        string settingsFolderPath = Path.Combine(DataLocation.DataDirectory(), Constant.Settings);
        string parentFolderPath = Path.GetDirectoryName(settingsFolderPath);
        PluginManager.API.OpenDirectory(parentFolderPath);
    }


    [RelayCommand]
    private void OpenLogsFolder()
    {
        App.API.OpenDirectory(GetLogDir(Constant.Version).FullName);
    }

    [RelayCommand]
    private Task UpdateApp() => _updater.UpdateAppAsync(App.API, false);

    private void ClearLogFolder()
    {
        var logDirectory = GetLogDir();
        var logFiles = GetLogFiles();

        logFiles.ForEach(f => f.Delete());

        logDirectory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
            .Where(dir => !Constant.Version.Equals(dir.Name))
            .ToList()
            .ForEach(dir => dir.Delete());

        OnPropertyChanged(nameof(LogFolderSize));
    }

    private static DirectoryInfo GetLogDir(string version = "")
    {
        return new DirectoryInfo(Path.Combine(DataLocation.DataDirectory(), Constant.Logs, version));
    }

    private static List<FileInfo> GetLogFiles(string version = "")
    {
        return GetLogDir(version).EnumerateFiles("*", SearchOption.AllDirectories).ToList();
    }

    private static string BytesToReadableString(long bytes)
    {
        const int scale = 1024;
        string[] orders = { "GB", "MB", "KB", "B" };
        long max = (long)Math.Pow(scale, orders.Length - 1);

        foreach (string order in orders)
        {
            if (bytes > max)
                return $"{decimal.Divide(bytes, max):##.##} {order}";

            max /= scale;
        }

        return "0 B";
    }

}
