using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core;
using Flow.Launcher.Core.ExternalPlugins;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Core.Storage;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Plugin.SharedModels;
using Flow.Launcher.ViewModel;
using JetBrains.Annotations;
using Stopwatch = Flow.Launcher.Infrastructure.Stopwatch;

namespace Flow.Launcher
{
    public class PublicAPIInstance : IPublicAPI, IRemovable
    {
        private static readonly string ClassName = nameof(PublicAPIInstance);

        private readonly Settings _settings;
        private readonly MainViewModel _mainVM;

        // Must use getter to avoid accessing Application.Current.Resources.MergedDictionaries so earlier in theme constructor
        private Theme _theme;
        private Theme Theme => _theme ??= Ioc.Default.GetRequiredService<Theme>();

        // Must use getter to avoid circular dependency
        private Updater _updater;
        private Updater Updater => _updater ??= Ioc.Default.GetRequiredService<Updater>();

        private readonly object _saveSettingsLock = new();

        #region Constructor

        public PublicAPIInstance(Settings settings, MainViewModel mainVM)
        {
            _settings = settings;
            _mainVM = mainVM;
            GlobalHotkey.hookedKeyboardCallback = KListener_hookedKeyboardCallback;
            WebRequest.RegisterPrefix("data", new DataWebRequestFactory());
        }

        #endregion

        #region Public API

        public void ChangeQuery(string query, bool requery = false)
        {
            _mainVM.ChangeQueryText(query, requery);
        }

        public void RestartApp() => RestartApp(false);

        public void RestartAppAsAdmin() => RestartApp(true);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void RestartApp(bool runAsAdmin)
        {
            _mainVM.Hide();

            // We must manually save
            // UpdateManager.RestartApp() will call Environment.Exit(0)
            // which will cause ungraceful exit
            SaveAppAllSettings();

            // Wait for all image caches to be saved before restarting
            await ImageLoader.WaitSaveAsync();

            // Restart requires Squirrel's Update.exe to be present in the parent folder, 
            // it is only published from the project's release pipeline. When debugging without it,
            // the project may not restart or just terminates. This is expected.
            App.RestartApp(runAsAdmin);
        }

        public void ShowMainWindow() => _mainVM.Show();

        public void FocusQueryTextBox() => _mainVM.FocusQueryTextBox();

        public void HideMainWindow() => _mainVM.Hide();

        public bool IsMainWindowVisible() => _mainVM.MainWindowVisibilityStatus;

        public event VisibilityChangedEventHandler VisibilityChanged
        {
            add => _mainVM.VisibilityChanged += value;
            remove => _mainVM.VisibilityChanged -= value;
        }

        public void CheckForNewUpdate() => _ = Updater.UpdateAppAsync(false);

        public void SaveAppAllSettings()
        {
            lock (_saveSettingsLock)
            {
                _settings.Save();
                PluginManager.Save();
                _mainVM.Save();
            }
            _ = ImageLoader.SaveAsync();
        }

        public Task ReloadAllPluginData() => PluginManager.ReloadDataAsync();

        public void ShowMsgError(string title, string subTitle = "") =>
            ShowMsg(title, subTitle, Constant.ErrorIcon, true);

        public void ShowMsgErrorWithButton(string title, string buttonText, Action buttonAction, string subTitle = "") =>
            ShowMsgWithButton(title, buttonText, buttonAction, subTitle, Constant.ErrorIcon, true);

        public void ShowMsg(string title, string subTitle = "", string iconPath = "") =>
            ShowMsg(title, subTitle, iconPath, true);

        public void ShowMsg(string title, string subTitle, string iconPath, bool useMainWindowAsOwner = true)
        {
            Notification.Show(title, subTitle, iconPath);
        }

        public void ShowMsgWithButton(string title, string buttonText, Action buttonAction, string subTitle = "", string iconPath = "") =>
            ShowMsgWithButton(title, buttonText, buttonAction, subTitle, iconPath, true);

        public void ShowMsgWithButton(string title, string buttonText, Action buttonAction, string subTitle, string iconPath, bool useMainWindowAsOwner = true)
        {
            Notification.ShowWithButton(title, buttonText, buttonAction, subTitle, iconPath);
        }

        public void OpenSettingDialog()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                SettingWindow sw = SingletonWindowOpener.Open<SettingWindow>();
            });
        }

        public void ShellRun(string cmd, string filename = "cmd.exe")
        {
            var args = filename == "cmd.exe" ? $"/C {cmd}" : $"{cmd}";

            var startInfo = ShellCommand.SetProcessStartInfo(filename, arguments: args, createNoWindow: true);
            ShellCommand.Execute(startInfo);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        public async void CopyToClipboard(string stringToCopy, bool directCopy = false, bool showDefaultNotification = true)
        {
            if (string.IsNullOrEmpty(stringToCopy))
            {
                return;
            }

            var isFile = File.Exists(stringToCopy);
            if (directCopy && (isFile || Directory.Exists(stringToCopy)))
            {
                // Sometimes the clipboard is locked and cannot be accessed,
                // we need to retry a few times before giving up
                var exception = await RetryActionOnSTAThreadAsync(() =>
                {
                    var paths = new StringCollection
                    {
                        stringToCopy
                    };

                    Clipboard.SetFileDropList(paths);
                });
                
                if (exception == null)
                {
                    if (showDefaultNotification)
                    {
                        ShowMsg(
                            $"{GetTranslation("copy")} {(isFile ? GetTranslation("fileTitle") : GetTranslation("folderTitle"))}",
                            GetTranslation("completedSuccessfully"));
                    }
                }
                else
                {
                    LogException(nameof(PublicAPIInstance), "Failed to copy file/folder to clipboard", exception);
                    ShowMsgError(GetTranslation("failedToCopy"));
                }
            }
            else
            {
                // Sometimes the clipboard is locked and cannot be accessed,
                // we need to retry a few times before giving up
                var exception = await RetryActionOnSTAThreadAsync(() =>
                {
                    // We should use SetText instead of SetDataObject to avoid the clipboard being locked by other applications
                    Clipboard.SetText(stringToCopy);
                });

                if (exception == null)
                {
                    if (showDefaultNotification)
                    {
                        ShowMsg(
                            $"{GetTranslation("copy")} {GetTranslation("textTitle")}",
                            GetTranslation("completedSuccessfully"));
                    }
                }
                else
                {
                    LogException(nameof(PublicAPIInstance), "Failed to copy text to clipboard", exception);
                    ShowMsgError(GetTranslation("failedToCopy"));
                }  
            }
        }

        private static async Task<Exception> RetryActionOnSTAThreadAsync(Action action, int retryCount = 6, int retryDelay = 150)
        {
            for (var i = 0; i < retryCount; i++)
            {
                try
                {
                    await Win32Helper.StartSTATaskAsync(action).ConfigureAwait(false);
                    break;
                }
                catch (Exception e)
                {
                    if (i == retryCount - 1)
                    {
                        return e;
                    }
                    await Task.Delay(retryDelay);
                }
            }
            return null;
        }

        public void StartLoadingBar() => _mainVM.ProgressBarVisibility = Visibility.Visible;

        public void StopLoadingBar() => _mainVM.ProgressBarVisibility = Visibility.Collapsed;

        public string GetTranslation(string key) => Internationalization.GetTranslation(key);

        public List<PluginPair> GetAllPlugins() => PluginManager.AllPlugins.ToList();

        public MatchResult FuzzySearch(string query, string stringToCompare) =>
            StringMatcher.FuzzySearch(query, stringToCompare);

        public Task<string> HttpGetStringAsync(string url, CancellationToken token = default) =>
            Http.GetAsync(url, token);

        public Task<Stream> HttpGetStreamAsync(string url, CancellationToken token = default) =>
            Http.GetStreamAsync(url, token);

        public Task HttpDownloadAsync([NotNull] string url, [NotNull] string filePath, Action<double> reportProgress = null,
            CancellationToken token = default) => Http.DownloadAsync(url, filePath, reportProgress, token);

        public void AddActionKeyword(string pluginId, string newActionKeyword) =>
            PluginManager.AddActionKeyword(pluginId, newActionKeyword);

        public bool ActionKeywordAssigned(string actionKeyword) => PluginManager.ActionKeywordRegistered(actionKeyword);

        public void RemoveActionKeyword(string pluginId, string oldActionKeyword) =>
            PluginManager.RemoveActionKeyword(pluginId, oldActionKeyword);

        public void LogDebug(string className, string message, [CallerMemberName] string methodName = "") =>
            Log.Debug(className, message, methodName);

        public void LogInfo(string className, string message, [CallerMemberName] string methodName = "") =>
            Log.Info(className, message, methodName);

        public void LogWarn(string className, string message, [CallerMemberName] string methodName = "") =>
            Log.Warn(className, message, methodName);

        public void LogError(string className, string message, [CallerMemberName] string methodName = "") =>
            Log.Error(className, message, methodName);

        public void LogException(string className, string message, Exception e, [CallerMemberName] string methodName = "") =>
            Log.Exception(className, message, e, methodName);

        private readonly ConcurrentDictionary<Type, ISavable> _pluginJsonStorages = new();

        public void RemovePluginSettings(string assemblyName)
        {
            foreach (var keyValuePair in _pluginJsonStorages)
            {
                var key = keyValuePair.Key;
                var value = keyValuePair.Value;
                var name = value.GetType().GetField("AssemblyName")?.GetValue(value)?.ToString();
                if (name == assemblyName)
                {
                    _pluginJsonStorages.TryRemove(key, out var _);
                }
            }
        }

        public void SavePluginSettings()
        {
            foreach (var savable in _pluginJsonStorages.Values)
            {
                savable.Save();
            }
        }

        public T LoadSettingJsonStorage<T>() where T : new()
        {
            var type = typeof(T);
            if (!_pluginJsonStorages.ContainsKey(type))
                _pluginJsonStorages[type] = new PluginJsonStorage<T>();

            return ((PluginJsonStorage<T>)_pluginJsonStorages[type]).Load();
        }

        public void SaveSettingJsonStorage<T>() where T : new()
        {
            var type = typeof(T);
            if (!_pluginJsonStorages.ContainsKey(type))
                _pluginJsonStorages[type] = new PluginJsonStorage<T>();

            ((PluginJsonStorage<T>)_pluginJsonStorages[type]).Save();
        }
        
        public void OpenDirectory(string directoryPath, string fileNameOrFilePath = null)
        {
            try
            {
                var explorerInfo = _settings.CustomExplorer;
                var explorerPath = explorerInfo.Path.Trim().ToLowerInvariant();
                var targetPath = fileNameOrFilePath is null
                    ? directoryPath
                    : Path.IsPathRooted(fileNameOrFilePath)
                        ? fileNameOrFilePath
                        : Path.Combine(directoryPath, fileNameOrFilePath);

                if (Path.GetFileNameWithoutExtension(explorerPath) == "explorer")
                {
                    // Windows File Manager
                    if (fileNameOrFilePath is null)
                    {
                        // Only Open the directory
                        using var explorer = new Process();
                        explorer.StartInfo = new ProcessStartInfo
                        {
                            FileName = directoryPath,
                            UseShellExecute = true
                        };
                        explorer.Start();
                    }
                    else
                    {
                        // Open the directory and select the file
                        Win32Helper.OpenFolderAndSelectFile(targetPath);
                    }
                }
                else
                {
                    // Custom File Manager
                    using var explorer = new Process();
                    explorer.StartInfo = new ProcessStartInfo
                    {
                        FileName = explorerInfo.Path.Replace("%d", directoryPath),
                        UseShellExecute = true,
                        Arguments = fileNameOrFilePath is null
                            ? explorerInfo.DirectoryArgument.Replace("%d", directoryPath)
                            : explorerInfo.FileArgument
                                .Replace("%d", directoryPath)
                                .Replace("%f", targetPath)
                    };
                    explorer.Start();
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
            {
                LogError(ClassName, "File Manager not found");
                ShowMsgBox(
                    string.Format(GetTranslation("fileManagerNotFound"), ex.Message),
                    GetTranslation("fileManagerNotFoundTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            catch (Exception ex)
            {
                LogException(ClassName, "Failed to open folder", ex);
                ShowMsgBox(
                    string.Format(GetTranslation("folderOpenError"), ex.Message),
                    GetTranslation("errorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void OpenUri(Uri uri, bool? inPrivate = null)
        {
            if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            {
                var browserInfo = _settings.CustomBrowser;

                var path = browserInfo.Path == "*" ? "" : browserInfo.Path;

                try
                {
                    if (browserInfo.OpenInTab)
                    {
                        uri.AbsoluteUri.OpenInBrowserTab(path, inPrivate ?? browserInfo.EnablePrivate, browserInfo.PrivateArg);
                    }
                    else
                    {
                        uri.AbsoluteUri.OpenInBrowserWindow(path, inPrivate ?? browserInfo.EnablePrivate, browserInfo.PrivateArg);
                    }
                }
                catch (Exception e)
                {
                    var tabOrWindow = browserInfo.OpenInTab ? "tab" : "window";
                    LogException(ClassName, $"Failed to open URL in browser {tabOrWindow}: {path}, {inPrivate ?? browserInfo.EnablePrivate}, {browserInfo.PrivateArg}", e);
                    ShowMsgBox(
                        GetTranslation("browserOpenError"),
                        GetTranslation("errorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            else
            {
                StartProcess(uri.AbsoluteUri, arguments: string.Empty, useShellExecute: true);
            }
        }

        public void OpenUrl(string url, bool? inPrivate = null)
        {
            OpenUri(new Uri(url), inPrivate);
        }

        public void OpenUrl(Uri url, bool? inPrivate = null)
        {
            OpenUri(url, inPrivate);
        }

        public void OpenAppUri(string appUri)
        {
            OpenUri(new Uri(appUri));
        }

        public void OpenAppUri(Uri appUri)
        {
            OpenUri(appUri);
        }

        public void ToggleGameMode() 
        {
            _mainVM.ToggleGameMode();
        }

        public void SetGameMode(bool value)
        {
            _mainVM.GameModeStatus = value;
        }

        public bool IsGameModeOn()
        {
            return _mainVM.GameModeStatus;
        }

        private readonly List<Func<int, int, SpecialKeyState, bool>> _globalKeyboardHandlers = new();

        public void RegisterGlobalKeyboardCallback(Func<int, int, SpecialKeyState, bool> callback) =>
            _globalKeyboardHandlers.Add(callback);

        public void RemoveGlobalKeyboardCallback(Func<int, int, SpecialKeyState, bool> callback) =>
            _globalKeyboardHandlers.Remove(callback);

        public void ReQuery(bool reselect = true) => _mainVM.ReQuery(reselect);

        public void BackToQueryResults() => _mainVM.BackToQueryResults();

        public MessageBoxResult ShowMsgBox(string messageBoxText, string caption = "",
            MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.OK) =>
            MessageBoxEx.Show(messageBoxText, caption, button, icon, defaultResult);

        public Task ShowProgressBoxAsync(string caption, Func<Action<double>, Task> reportProgressAsync,
            Action cancelProgress = null) => ProgressBoxEx.ShowAsync(caption, reportProgressAsync, cancelProgress);

        public List<ThemeData> GetAvailableThemes() => Theme.GetAvailableThemes();

        public ThemeData GetCurrentTheme() => Theme.GetCurrentTheme();

        public bool SetCurrentTheme(ThemeData theme) =>
            Theme.ChangeTheme(theme.FileNameWithoutExtension);

        private readonly ConcurrentDictionary<(string, string, Type), ISavable> _pluginBinaryStorages = new();

        public void RemovePluginCaches(string cacheDirectory)
        {
            foreach (var keyValuePair in _pluginBinaryStorages)
            {
                var key = keyValuePair.Key;
                var currentCacheDirectory = key.Item2;
                if (cacheDirectory == currentCacheDirectory)
                {
                    _pluginBinaryStorages.TryRemove(key, out var _);
                }
            }
        }

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
            if (!_pluginBinaryStorages.ContainsKey((cacheName, cacheDirectory, type)))
                _pluginBinaryStorages[(cacheName, cacheDirectory, type)] = new PluginBinaryStorage<T>(cacheName, cacheDirectory);

            return await ((PluginBinaryStorage<T>)_pluginBinaryStorages[(cacheName, cacheDirectory, type)]).TryLoadAsync(defaultData);
        }

        public async Task SaveCacheBinaryStorageAsync<T>(string cacheName, string cacheDirectory) where T : new()
        {
            var type = typeof(T);
            if (!_pluginBinaryStorages.ContainsKey((cacheName, cacheDirectory, type)))
                _pluginBinaryStorages[(cacheName, cacheDirectory, type)] = new PluginBinaryStorage<T>(cacheName, cacheDirectory);

            await ((PluginBinaryStorage<T>)_pluginBinaryStorages[(cacheName, cacheDirectory, type)]).SaveAsync();
        }

        public ValueTask<ImageSource> LoadImageAsync(string path, bool loadFullImage = false, bool cacheImage = true) =>
            ImageLoader.LoadAsync(path, loadFullImage, cacheImage);

        public Task<bool> UpdatePluginManifestAsync(bool usePrimaryUrlOnly = false, CancellationToken token = default) =>
            PluginsManifest.UpdateManifestAsync(usePrimaryUrlOnly, token);

        public IReadOnlyList<UserPlugin> GetPluginManifest() => PluginsManifest.UserPlugins;

        public bool PluginModified(string id) => PluginManager.PluginModified(id);

        public Task UpdatePluginAsync(PluginMetadata pluginMetadata, UserPlugin plugin, string zipFilePath) =>
            PluginManager.UpdatePluginAsync(pluginMetadata, plugin, zipFilePath);

        public void InstallPlugin(UserPlugin plugin, string zipFilePath) =>
            PluginManager.InstallPlugin(plugin, zipFilePath);

        public Task UninstallPluginAsync(PluginMetadata pluginMetadata, bool removePluginSettings = false) =>
            PluginManager.UninstallPluginAsync(pluginMetadata, removePluginSettings);

        public long StopwatchLogDebug(string className, string message, Action action, [CallerMemberName] string methodName = "") =>
            Stopwatch.Debug(className, message, action, methodName);

        public Task<long> StopwatchLogDebugAsync(string className, string message, Func<Task> action, [CallerMemberName] string methodName = "") =>
            Stopwatch.DebugAsync(className, message, action, methodName);

        public long StopwatchLogInfo(string className, string message, Action action, [CallerMemberName] string methodName = "") =>
            Stopwatch.Info(className, message, action, methodName);

        public Task<long> StopwatchLogInfoAsync(string className, string message, Func<Task> action, [CallerMemberName] string methodName = "") =>
            Stopwatch.InfoAsync(className, message, action, methodName);

        public bool StartProcess(string filePath, string workingDirectory = "", string arguments = "", bool useShellExecute = false, string verb = "")
        {
            try
            {
                workingDirectory = string.IsNullOrEmpty(workingDirectory) ? Environment.CurrentDirectory : workingDirectory;

                // Use command executer to run the process as desktop user if running as admin
                if (Win32Helper.IsAdministrator())
                {
                    var result = Win32Helper.RunAsDesktopUser(
                        Constant.CommandExecutablePath,
                        Environment.CurrentDirectory,
                        $"-StartProcess -FileName {AddDoubleQuotes(filePath)} -WorkingDirectory {AddDoubleQuotes(workingDirectory)} -Arguments {AddDoubleQuotes(arguments)} -UseShellExecute {useShellExecute} -Verb {AddDoubleQuotes(verb)}",
                        false,
                        true, // Do not show the command window
                        out var errorInfo);
                    if (!string.IsNullOrEmpty(errorInfo))
                    {
                        LogError(ClassName, $"Failed to start process {filePath} with error: {errorInfo}");
                    }

                    return result;
                }

                var info = new ProcessStartInfo
                {
                    FileName = filePath,
                    WorkingDirectory = workingDirectory,
                    Arguments = arguments,
                    UseShellExecute = useShellExecute,
                    Verb = verb,
                };
                Process.Start(info)?.Dispose();
                return true;
            }
            catch (Exception e)
            {
                LogException(ClassName, $"Failed to start process {filePath} with arguments {arguments} under {workingDirectory}", e);
                return false;
            }
        }

        public bool StartProcess(string filePath, string workingDirectory = "", Collection<string> argumentList = null, bool useShellExecute = false, string verb = "") =>
            StartProcess(filePath, workingDirectory, JoinArgumentList(argumentList), useShellExecute, verb);

        private static string AddDoubleQuotes(string arg)
        {
            if (string.IsNullOrEmpty(arg))
                return "\"\"";

            // If already wrapped in double quotes, return as is
            if (arg.Length >= 2 && arg[0] == '"' && arg[^1] == '"')
                return arg;

            return $"\"{arg}\"";
        }

        private static string JoinArgumentList(Collection<string> args)
        {
            if (args == null || args.Count == 0)
                return string.Empty;

            return string.Join(" ", args.Select(arg =>
            {
                if (string.IsNullOrEmpty(arg))
                    return "\"\"";

                // Add double quotes
                return AddDoubleQuotes(arg);
            }));
        }

        #endregion

        #region Private Methods

        private bool KListener_hookedKeyboardCallback(KeyEvent keyevent, int vkcode, SpecialKeyState state)
        {
            var continueHook = true;
            foreach (var x in _globalKeyboardHandlers)
            {
                continueHook &= x((int)keyevent, vkcode, state);
            }

            return continueHook;
        }

        #endregion
    }
}
