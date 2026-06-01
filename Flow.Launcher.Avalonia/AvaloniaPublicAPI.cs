using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Flow.Launcher.Avalonia.Views.Dialogs;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;
using Flow.Launcher.Avalonia.Views.Controls;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.ExternalPlugins;
using Flow.Launcher.Avalonia.ViewModel;
using Flow.Launcher.Avalonia.Resource;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Flow.Launcher.Avalonia;

/// <summary>
/// Minimal IPublicAPI for Avalonia - just enough for plugin queries to work.
/// </summary>
public class AvaloniaPublicAPI : IPublicAPI
{
    private readonly Settings _settings;
    private readonly Func<MainViewModel> _getMainViewModel;
    private readonly Internationalization _i18n;
    private readonly Dictionary<Type, ISavable> _pluginJsonStorages = new();

    public AvaloniaPublicAPI(Settings settings, Func<MainViewModel> getMainViewModel, Internationalization i18n)
    {
        _settings = settings;
        _getMainViewModel = getMainViewModel;
        _i18n = i18n;
    }

#pragma warning disable CS0067
    public event VisibilityChangedEventHandler? VisibilityChanged;
    public event ActualApplicationThemeChangedEventHandler? ActualApplicationThemeChanged;
#pragma warning restore CS0067

    // Essential for plugins
    public void ChangeQuery(string query, bool requery = false)
    {
        var mainViewModel = _getMainViewModel();
        if (requery && string.Equals(mainViewModel.QueryText, query, StringComparison.Ordinal))
        {
            ReQuery();
            return;
        }

        mainViewModel.QueryText = query;
    }
    
    public string GetTranslation(string key) => _i18n.GetTranslation(key);
    
    public List<PluginPair> GetAllPlugins() => PluginManager.GetAllLoadedPlugins();
    public List<PluginPair> GetAllInitializedPlugins(bool includeFailed) => PluginManager.GetAllInitializedPlugins(includeFailed);
    public MatchResult FuzzySearch(string query, string stringToCompare) => 
        Ioc.Default.GetRequiredService<StringMatcher>().FuzzyMatch(query, stringToCompare);

    // Logging
    public void LogDebug(string className, string message, [CallerMemberName] string methodName = "") => Log.Debug(className, message, methodName);
    public void LogInfo(string className, string message, [CallerMemberName] string methodName = "") => Log.Info(className, message, methodName);
    public void LogWarn(string className, string message, [CallerMemberName] string methodName = "") => Log.Warn(className, message, methodName);
    public void LogError(string className, string message, [CallerMemberName] string methodName = "") => Log.Error(className, message, methodName);
    public void LogException(string className, string message, Exception e, [CallerMemberName] string methodName = "") => Log.Exception(className, message, e, methodName);

    // Shell/URL operations
    public void ShellRun(string cmd, string filename = "cmd.exe") => 
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = filename, Arguments = $"/c {cmd}", UseShellExecute = true });
    public void OpenUrl(string url, bool? inPrivate = null) => 
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
    public void OpenUrl(Uri url, bool? inPrivate = null) => OpenUrl(url.ToString(), inPrivate);
    public void OpenWebUrl(string url, bool? inPrivate = null) => OpenUrl(url, inPrivate);
    public void OpenWebUrl(Uri url, bool? inPrivate = null) => OpenUrl(url.ToString(), inPrivate);
    public void OpenAppUri(Uri appUri) => OpenUrl(appUri);
    public void OpenAppUri(string appUri) => OpenUrl(appUri);
    public void OpenDirectory(string DirectoryPath, string? FileNameOrFilePath = null)
    {
        if (FileNameOrFilePath is null)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = DirectoryPath,
                UseShellExecute = true
            });
            return;
        }

        var targetPath = Path.IsPathRooted(FileNameOrFilePath)
            ? FileNameOrFilePath
            : Path.Combine(DirectoryPath, FileNameOrFilePath);

        Win32Helper.OpenFolderAndSelectFile(targetPath);
    }

    // Clipboard
    public void CopyToClipboard(string text, bool directCopy = false, bool showDefaultNotification = true)
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow?.Clipboard?.SetTextAsync(text);
    }

    // HTTP (delegate to Infrastructure)
    public Task<string> HttpGetStringAsync(string url, CancellationToken token = default) => Infrastructure.Http.Http.GetAsync(url, token);
    public Task<Stream> HttpGetStreamAsync(string url, CancellationToken token = default) => Infrastructure.Http.Http.GetStreamAsync(url, token);
    public Task HttpDownloadAsync(string url, string filePath, Action<double>? reportProgress = null, CancellationToken token = default) => 
        Infrastructure.Http.Http.DownloadAsync(url, filePath, reportProgress, token);

    // Plugin management
    public void AddActionKeyword(string pluginId, string newActionKeyword) => PluginManager.AddActionKeyword(pluginId, newActionKeyword);
    public void RemoveActionKeyword(string pluginId, string oldActionKeyword) => PluginManager.RemoveActionKeyword(pluginId, oldActionKeyword);
    public bool ActionKeywordAssigned(string actionKeyword) => PluginManager.ActionKeywordRegistered(actionKeyword);
    public bool PluginModified(string id) => PluginManager.PluginModified(id);

    // Paths
    public string GetDataDirectory() => DataLocation.DataDirectory();
    public string GetLogDirectory() => Log.CurrentLogDirectory;

    // Stubs - not critical for basic queries
    public void RestartApp() { }
    public void SaveAppAllSettings() { }
    public void SavePluginSettings() { }
    public Task ReloadAllPluginData() => Task.CompletedTask;
    public void CheckForNewUpdate() { }
    public void ShowMsgError(string title, string subTitle = "") => ShowMsg(title, subTitle, Constant.ErrorIcon, true);
    public void ShowMsgErrorWithButton(string title, string buttonText, Action buttonAction, string subTitle = "") =>
        ShowMsgWithButton(title, buttonText, buttonAction, subTitle, Constant.ErrorIcon, true);
    public void ShowMainWindow() { }
    public void FocusQueryTextBox() { }
    public void HideMainWindow() => _getMainViewModel()?.RequestHide();
    public bool IsMainWindowVisible() => true;
    public void ShowMsg(string title, string subTitle = "", string iconPath = "") =>
        ShowMsg(title, subTitle, iconPath, true);
    public void ShowMsg(string title, string subTitle, string iconPath, bool useMainWindowAsOwner = true)
    {
        NotificationWindow.ShowNotification(title, subTitle, iconPath);
    }
    public void ShowMsgWithButton(string title, string buttonText, Action buttonAction, string subTitle = "", string iconPath = "") =>
        ShowMsgWithButton(title, buttonText, buttonAction, subTitle, iconPath, true);
    public void ShowMsgWithButton(string title, string buttonText, Action buttonAction, string subTitle, string iconPath, bool useMainWindowAsOwner = true)
    {
        NotificationWindow.ShowNotification(title, subTitle, iconPath, buttonText, buttonAction);
    }
    public void OpenSettingDialog() => _getMainViewModel()?.OpenSettings();
    public bool OpenPluginSettingsWindow(string pluginId)
    {
        try
        {
            var plugin = PluginManager.GetPluginForId(pluginId);
            if (plugin?.Plugin is not ISettingProvider settingProvider)
            {
                return false;
            }

            if (plugin.Plugin is JsonRPCPluginBase jsonRpcPlugin && !jsonRpcPlugin.NeedCreateSettingPanel())
            {
                return false;
            }

            if (settingProvider.CreateSettingPanelAvalonia() != null)
            {
                OpenSettingDialog();
                return true;
            }

            var settingsControl = settingProvider.CreateSettingPanel();
            if (settingsControl == null)
            {
                return false;
            }

            WpfSettingsWindow.Show(settingsControl, plugin.Metadata.Name);
            return true;
        }
        catch (Exception e)
        {
            Log.Exception(nameof(AvaloniaPublicAPI), $"Failed to open plugin settings window for plugin id '{pluginId}'", e);
            return false;
        }
    }
    public void RegisterGlobalKeyboardCallback(Func<int, int, SpecialKeyState, bool> callback) { }
    public void RemoveGlobalKeyboardCallback(Func<int, int, SpecialKeyState, bool> callback) { }
    public T LoadSettingJsonStorage<T>() where T : new()
    {
        var type = typeof(T);
        if (!_pluginJsonStorages.ContainsKey(type))
        {
            _pluginJsonStorages[type] = new PluginJsonStorage<T>();
        }

        return ((PluginJsonStorage<T>)_pluginJsonStorages[type]).Load();
    }

    public void SaveSettingJsonStorage<T>() where T : new()
    {
        var type = typeof(T);
        if (!_pluginJsonStorages.ContainsKey(type))
        {
            _pluginJsonStorages[type] = new PluginJsonStorage<T>();
        }

        ((PluginJsonStorage<T>)_pluginJsonStorages[type]).Save();
    }
    public void ToggleGameMode() { }
    public void SetGameMode(bool value) { }
    public bool IsGameModeOn() => false;
    public void ReQuery(bool reselect = true) { var q = _getMainViewModel().QueryText; _getMainViewModel().QueryText = ""; _getMainViewModel().QueryText = q; }
    public void BackToQueryResults() { }
    public MessageBoxResult ShowMsgBox(string messageBoxText, string caption = "", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.OK)
    {
        if (System.Windows.Application.Current?.Dispatcher != null)
        {
            return System.Windows.Application.Current.Dispatcher.Invoke(() =>
                System.Windows.MessageBox.Show(messageBoxText, caption, button, icon, defaultResult));
        }

        return System.Windows.MessageBox.Show(messageBoxText, caption, button, icon, defaultResult);
    }
    public Task ShowProgressBoxAsync(string caption, Func<Action<double>, Task> reportProgressAsync, Action? cancelProgress = null) =>
        ProgressBoxWindow.ShowAsync(caption, reportProgressAsync, cancelProgress);
    public void StartLoadingBar() => _getMainViewModel().IsQueryRunning = true;
    public void StopLoadingBar() => _getMainViewModel().IsQueryRunning = false;
    public List<ThemeData> GetAvailableThemes() => new();
    public ThemeData? GetCurrentTheme() => null;
    public bool SetCurrentTheme(ThemeData theme) => false;
    public void SavePluginCaches() { }
    public Task<T> LoadCacheBinaryStorageAsync<T>(string cacheName, string cacheDirectory, T defaultData) where T : new() => Task.FromResult(defaultData);
    public Task SaveCacheBinaryStorageAsync<T>(string cacheName, string cacheDirectory) where T : new() => Task.CompletedTask;
    public ValueTask<ImageSource> LoadImageAsync(string path, bool loadFullImage = false, bool cacheImage = true) => new((ImageSource)null!);
    public Task<bool> UpdatePluginManifestAsync(bool usePrimaryUrlOnly = false, CancellationToken token = default) => 
        PluginsManifest.UpdateManifestAsync(usePrimaryUrlOnly, token);
    public IReadOnlyList<UserPlugin> GetPluginManifest() => PluginsManifest.UserPlugins ?? new List<UserPlugin>();
    public Task<bool> UpdatePluginAsync(PluginMetadata pluginMetadata, UserPlugin plugin, string zipFilePath) => Task.FromResult(false);
    public bool InstallPlugin(UserPlugin plugin, string zipFilePath) => false;
    public Task<bool> UninstallPluginAsync(PluginMetadata pluginMetadata, bool removePluginSettings = false) => Task.FromResult(false);
    public bool IsApplicationDarkTheme() => true;

    public long StopwatchLogDebug(string className, string message, Action action, [CallerMemberName] string methodName = "")
    { var sw = System.Diagnostics.Stopwatch.StartNew(); action(); sw.Stop(); LogDebug(className, $"{message}: {sw.ElapsedMilliseconds}ms", methodName); return sw.ElapsedMilliseconds; }
    public async Task<long> StopwatchLogDebugAsync(string className, string message, Func<Task> action, [CallerMemberName] string methodName = "")
    { var sw = System.Diagnostics.Stopwatch.StartNew(); await action(); sw.Stop(); LogDebug(className, $"{message}: {sw.ElapsedMilliseconds}ms", methodName); return sw.ElapsedMilliseconds; }
    public long StopwatchLogInfo(string className, string message, Action action, [CallerMemberName] string methodName = "")
    { var sw = System.Diagnostics.Stopwatch.StartNew(); action(); sw.Stop(); LogInfo(className, $"{message}: {sw.ElapsedMilliseconds}ms", methodName); return sw.ElapsedMilliseconds; }
    public async Task<long> StopwatchLogInfoAsync(string className, string message, Func<Task> action, [CallerMemberName] string methodName = "")
    { var sw = System.Diagnostics.Stopwatch.StartNew(); await action(); sw.Stop(); LogInfo(className, $"{message}: {sw.ElapsedMilliseconds}ms", methodName); return sw.ElapsedMilliseconds; }
}
