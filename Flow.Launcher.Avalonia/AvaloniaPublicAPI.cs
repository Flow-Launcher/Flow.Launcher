using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Flow.Launcher.Avalonia.Views.Dialogs;
using Flow.Launcher.Core;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Avalonia.Views.Controls;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.ExternalPlugins;
using Flow.Launcher.Avalonia.ViewModel;
using Flow.Launcher.Avalonia.Resource;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Flow.Launcher.Avalonia;

/// <summary>
/// IPublicAPI implementation for the Avalonia host.
/// </summary>
public class AvaloniaPublicAPI : IPublicAPI
{
    private readonly Settings _settings;
    private readonly Func<MainViewModel> _getMainViewModel;
    private readonly Internationalization _i18n;
    private readonly object _saveSettingsLock = new();
    private readonly ConcurrentDictionary<Type, ISavable> _pluginJsonStorages = new();
    private readonly ConcurrentDictionary<(string, string, Type), ISavable> _pluginBinaryStorages = new();
    private readonly object _globalKeyboardHandlersLock = new();
    private readonly List<Func<int, int, SpecialKeyState, bool>> _globalKeyboardHandlers = new();
    private Flow.Launcher.Core.Resource.Theme? _theme;
    private bool _gameModeStatus;

    public AvaloniaPublicAPI(Settings settings, Func<MainViewModel> getMainViewModel, Internationalization i18n)
    {
        _settings = settings;
        _getMainViewModel = getMainViewModel;
        _i18n = i18n;
        Flow.Launcher.Infrastructure.Hotkey.GlobalHotkey.hookedKeyboardCallback = KListenerHookedKeyboardCallback;
    }

#pragma warning disable CS0067
    public event VisibilityChangedEventHandler? VisibilityChanged;
    public event ActualApplicationThemeChangedEventHandler? ActualApplicationThemeChanged;
#pragma warning restore CS0067
    public event EventHandler StringMatcherBehaviorChanged
    {
        add => _settings.StringMatcherBehaviorChanged += value;
        remove => _settings.StringMatcherBehaviorChanged -= value;
    }


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
    public void OpenUrl(string url, bool? inPrivate = null) => OpenUri(new Uri(url), inPrivate);
    public void OpenUrl(Uri url, bool? inPrivate = null) => OpenUri(url, inPrivate);
    public void OpenWebUrl(string url, bool? inPrivate = null) => OpenUri(new Uri(url), inPrivate, true);
    public void OpenWebUrl(Uri url, bool? inPrivate = null) => OpenUri(url, inPrivate, true);
    public void OpenAppUri(Uri appUri) => OpenUri(appUri);
    public void OpenAppUri(string appUri) => OpenUri(new Uri(appUri));

    public void OpenDirectory(string DirectoryPath, string? FileNameOrFilePath = null)
    {
        try
        {
            var explorerInfo = _settings.CustomExplorer;
            var explorerPath = explorerInfo.Path.Trim().ToLowerInvariant();
            var targetPath = FileNameOrFilePath is null
                ? DirectoryPath
                : Path.IsPathRooted(FileNameOrFilePath)
                    ? FileNameOrFilePath
                    : Path.Combine(DirectoryPath, FileNameOrFilePath);

            if (Path.GetFileNameWithoutExtension(explorerPath) == "explorer")
            {
                if (FileNameOrFilePath is null)
                {
                    using var explorer = new Process();
                    explorer.StartInfo = new ProcessStartInfo
                    {
                        FileName = DirectoryPath,
                        UseShellExecute = true
                    };
                    explorer.Start();
                }
                else
                {
                    Win32Helper.OpenFolderAndSelectFile(targetPath);
                }
            }
            else
            {
                using var explorer = new Process();
                explorer.StartInfo = new ProcessStartInfo
                {
                    FileName = explorerInfo.Path.Replace("%d", DirectoryPath),
                    UseShellExecute = true,
                    Arguments = FileNameOrFilePath is null
                        ? explorerInfo.DirectoryArgument.Replace("%d", DirectoryPath)
                        : explorerInfo.FileArgument
                            .Replace("%d", DirectoryPath)
                            .Replace("%f", targetPath)
                };
                explorer.Start();
            }
        }
        catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x80004004))
        {
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            LogException(nameof(AvaloniaPublicAPI), "File manager not found", ex);
            ShowMsgError(GetTranslation("fileManagerNotFoundTitle"), GetTranslation("fileManagerNotFound"));
        }
        catch (Exception ex)
        {
            LogException(nameof(AvaloniaPublicAPI), "Failed to open folder", ex);
            ShowMsgError(GetTranslation("errorTitle"), GetTranslation("folderOpenError"));
        }
    }

    private void OpenUri(Uri uri, bool? inPrivate = null, bool forceBrowser = false)
    {
        if (uri.IsFile && !uri.LocalPath.FileOrLocationExists())
        {
            ShowMsgError(GetTranslation("errorTitle"), string.Format(GetTranslation("fileNotFoundError"), uri.LocalPath));
            return;
        }

        if (forceBrowser || uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        {
            var browserInfo = _settings.CustomBrowser;
            var path = browserInfo.Path == "*" ? string.Empty : browserInfo.Path;

            try
            {
                if (browserInfo.OpenInTab)
                {
                    uri.AbsoluteUri.OpenInBrowserTab(path, inPrivate ?? browserInfo.EnablePrivate, browserInfo.PrivateArg, browserInfo.ExtraArgs);
                }
                else
                {
                    uri.AbsoluteUri.OpenInBrowserWindow(path, inPrivate ?? browserInfo.EnablePrivate, browserInfo.PrivateArg, browserInfo.ExtraArgs);
                }
            }
            catch (Exception e)
            {
                var tabOrWindow = browserInfo.OpenInTab ? "tab" : "window";
                var includesExtraArgs = string.IsNullOrWhiteSpace(browserInfo.ExtraArgs)
                    ? string.Empty
                    : ", [including omitted Extra Args]";
                LogException(nameof(AvaloniaPublicAPI), $"Failed to open URL in browser {tabOrWindow}: {path}, {inPrivate ?? browserInfo.EnablePrivate}, {browserInfo.PrivateArg}{includesExtraArgs}", e);
                ShowMsgError(GetTranslation("errorTitle"), GetTranslation("browserOpenError"));
            }
        }
        else
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true
                })?.Dispose();
            }
            catch (Exception e)
            {
                LogException(nameof(AvaloniaPublicAPI), $"Failed to open: {uri.AbsoluteUri}", e);
                ShowMsgError(GetTranslation("errorTitle"), e.Message);
            }
        }
    }

    // Clipboard
    public async void CopyToClipboard(string text, bool directCopy = false, bool showDefaultNotification = true)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var isFile = File.Exists(text);
        if (directCopy && (isFile || Directory.Exists(text)))
        {
            var exception = await RetryActionOnStaThreadAsync(() =>
            {
                var paths = new StringCollection { text };
                Clipboard.SetFileDropList(paths);
            });

            if (exception == null)
            {
                if (showDefaultNotification)
                {
                    ShowMsg($"{GetTranslation("copy")} {(isFile ? GetTranslation("fileTitle") : GetTranslation("folderTitle"))}", GetTranslation("completedSuccessfully"));
                }
            }
            else
            {
                LogException(nameof(AvaloniaPublicAPI), "Failed to copy file/folder to clipboard", exception);
                ShowMsgError(GetTranslation("failedToCopy"));
            }

            return;
        }

        var textException = await RetryActionOnStaThreadAsync(() => Clipboard.SetText(text));
        if (textException == null)
        {
            if (showDefaultNotification)
            {
                ShowMsg($"{GetTranslation("copy")} {GetTranslation("textTitle")}", GetTranslation("completedSuccessfully"));
            }
        }
        else
        {
            LogException(nameof(AvaloniaPublicAPI), "Failed to copy text to clipboard", textException);
            ShowMsgError(GetTranslation("failedToCopy"));
        }
    }

    private static async Task<Exception?> RetryActionOnStaThreadAsync(Action action, int retryCount = 6, int retryDelay = 150)
    {
        for (var i = 0; i < retryCount; i++)
        {
            try
            {
                await Win32Helper.StartSTATaskAsync(action).ConfigureAwait(false);
                return null;
            }
            catch (Exception e)
            {
                if (i == retryCount - 1)
                {
                    return e;
                }

                await Task.Delay(retryDelay).ConfigureAwait(false);
            }
        }

        return null;
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

    public void RestartApp()
    {
        HideMainWindow();
        SaveAppAllSettings();

        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(exePath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true
            });
        }

        if (global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    public void SaveAppAllSettings()
    {
        lock (_saveSettingsLock)
        {
            _settings.Save();
            PluginManager.Save();
            _getMainViewModel().Save();
            SavePluginSettings();
            SavePluginCaches();
        }
    }

    public void SavePluginSettings()
    {
        foreach (var savable in _pluginJsonStorages.Values)
        {
            savable.Save();
        }
    }

    public Task ReloadAllPluginData() => PluginManager.ReloadDataAsync();
    public void CheckForNewUpdate() => _ = Ioc.Default.GetRequiredService<Updater>().UpdateAppAsync(false);
    public void ShowMsgError(string title, string subTitle = "") => ShowMsg(title, subTitle, Constant.ErrorIcon, true);
    public void ShowMsgErrorWithButton(string title, string buttonText, Action buttonAction, string subTitle = "") =>
        ShowMsgWithButton(title, buttonText, buttonAction, subTitle, Constant.ErrorIcon, true);
    public void ShowMainWindow() => _getMainViewModel()?.Show();
    public void FocusQueryTextBox() =>
        _getMainViewModel()?.RequestQueryTextFocus(new QueryTextFocusRequest(false, false, QueryTextFocusMode.CaretAtEnd));
    public void HideMainWindow() => _getMainViewModel()?.RequestHide();
    public bool IsMainWindowVisible() => _getMainViewModel()?.MainWindowVisibility ?? false;
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
    public void RegisterGlobalKeyboardCallback(Func<int, int, SpecialKeyState, bool> callback)
    {
        lock (_globalKeyboardHandlersLock)
        {
            _globalKeyboardHandlers.Add(callback);
        }
    }

    public void RemoveGlobalKeyboardCallback(Func<int, int, SpecialKeyState, bool> callback)
    {
        lock (_globalKeyboardHandlersLock)
        {
            _globalKeyboardHandlers.Remove(callback);
        }
    }

    public T LoadSettingJsonStorage<T>() where T : new()
    {
        var type = typeof(T);
        var storage = (PluginJsonStorage<T>)_pluginJsonStorages.GetOrAdd(type, _ => new PluginJsonStorage<T>());
        return storage.Load();
    }

    public void SaveSettingJsonStorage<T>() where T : new()
    {
        var type = typeof(T);
        var storage = (PluginJsonStorage<T>)_pluginJsonStorages.GetOrAdd(type, _ => new PluginJsonStorage<T>());
        storage.Save();
    }

    public void ToggleGameMode() => _gameModeStatus = !_gameModeStatus;
    public void SetGameMode(bool value) => _gameModeStatus = value;
    public bool IsGameModeOn() => _gameModeStatus;
    public void ReQuery(bool reselect = true) => _getMainViewModel().ReQuery(reselect);
    public void BackToQueryResults() => _getMainViewModel().BackToQueryResults();
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
    private Flow.Launcher.Core.Resource.Theme Theme => _theme ??= new Flow.Launcher.Core.Resource.Theme(this, _settings);
    public List<ThemeData> GetAvailableThemes() => Theme.GetAvailableThemes();
    public ThemeData GetCurrentTheme() => Theme.GetCurrentTheme();
    public bool SetCurrentTheme(ThemeData theme) => Theme.ChangeTheme(theme.FileNameWithoutExtension);

    public void SavePluginCaches()
    {
        foreach (var savable in _pluginBinaryStorages.Values)
        {
            savable.Save();
        }
    }

    public async Task<T> LoadCacheBinaryStorageAsync<T>(string cacheName, string cacheDirectory, T defaultData) where T : new()
    {
        var type = typeof(T);
        var storage = (PluginBinaryStorage<T>)_pluginBinaryStorages.GetOrAdd((cacheName, cacheDirectory, type),
            _ => new PluginBinaryStorage<T>(cacheName, cacheDirectory));

        try
        {
            return await storage.TryLoadAsync(defaultData);
        }
        catch (Exception e)
        {
            Log.Warn(nameof(AvaloniaPublicAPI), $"Failed to load cache '{cacheName}' from '{cacheDirectory}'. Falling back to default data. {e.Message}");
            return defaultData;
        }
    }

    public async Task SaveCacheBinaryStorageAsync<T>(string cacheName, string cacheDirectory) where T : new()
    {
        var type = typeof(T);
        var storage = (PluginBinaryStorage<T>)_pluginBinaryStorages.GetOrAdd((cacheName, cacheDirectory, type),
            _ => new PluginBinaryStorage<T>(cacheName, cacheDirectory));

        await storage.SaveAsync();
    }

    public ValueTask<ImageSource> LoadImageAsync(string path, bool loadFullImage = false, bool cacheImage = true) =>
        Flow.Launcher.Infrastructure.Image.ImageLoader.LoadAsync(path, loadFullImage, cacheImage);

    public Task<bool> UpdatePluginManifestAsync(bool usePrimaryUrlOnly = false, CancellationToken token = default) =>
        PluginsManifest.UpdateManifestAsync(usePrimaryUrlOnly, token);
    public IReadOnlyList<UserPlugin> GetPluginManifest() => PluginsManifest.UserPlugins ?? new List<UserPlugin>();
    public Task<bool> UpdatePluginAsync(PluginMetadata pluginMetadata, UserPlugin plugin, string zipFilePath) =>
        PluginManager.UpdatePluginAsync(pluginMetadata, plugin, zipFilePath);
    public bool InstallPlugin(UserPlugin plugin, string zipFilePath) => PluginManager.InstallPlugin(plugin, zipFilePath);
    public Task<bool> UninstallPluginAsync(PluginMetadata pluginMetadata, bool removePluginSettings = false) =>
        PluginManager.UninstallPluginAsync(pluginMetadata, removePluginSettings);
    public bool IsApplicationDarkTheme() =>
        global::Avalonia.Application.Current?.ActualThemeVariant == global::Avalonia.Styling.ThemeVariant.Dark;

    public long StopwatchLogDebug(string className, string message, Action action, [CallerMemberName] string methodName = "")
    { var sw = System.Diagnostics.Stopwatch.StartNew(); action(); sw.Stop(); LogDebug(className, $"{message}: {sw.ElapsedMilliseconds}ms", methodName); return sw.ElapsedMilliseconds; }
    public async Task<long> StopwatchLogDebugAsync(string className, string message, Func<Task> action, [CallerMemberName] string methodName = "")
    { var sw = System.Diagnostics.Stopwatch.StartNew(); await action(); sw.Stop(); LogDebug(className, $"{message}: {sw.ElapsedMilliseconds}ms", methodName); return sw.ElapsedMilliseconds; }
    public long StopwatchLogInfo(string className, string message, Action action, [CallerMemberName] string methodName = "")
    { var sw = System.Diagnostics.Stopwatch.StartNew(); action(); sw.Stop(); LogInfo(className, $"{message}: {sw.ElapsedMilliseconds}ms", methodName); return sw.ElapsedMilliseconds; }
    public async Task<long> StopwatchLogInfoAsync(string className, string message, Func<Task> action, [CallerMemberName] string methodName = "")
    { var sw = System.Diagnostics.Stopwatch.StartNew(); await action(); sw.Stop(); LogInfo(className, $"{message}: {sw.ElapsedMilliseconds}ms", methodName); return sw.ElapsedMilliseconds; }

    private bool KListenerHookedKeyboardCallback(KeyEvent keyEvent, int vkCode, SpecialKeyState state)
    {
        var continueHook = true;
        Func<int, int, SpecialKeyState, bool>[] handlers;
        lock (_globalKeyboardHandlersLock)
        {
            handlers = _globalKeyboardHandlers.ToArray();
        }

        foreach (var callback in handlers)
        {
            continueHook &= callback((int)keyEvent, vkCode, state);
        }

        return continueHook;
    }
}
