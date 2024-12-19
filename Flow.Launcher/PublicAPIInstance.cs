using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using Squirrel;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;
using Flow.Launcher.Plugin.SharedModels;
using Flow.Launcher.Plugin.SharedCommands;
using System.Threading;
using System.IO;
using Flow.Launcher.Infrastructure.Http;
using JetBrains.Annotations;
using System.Runtime.CompilerServices;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Collections.Specialized;
using Flow.Launcher.Core;

namespace Flow.Launcher
{
    public class PublicAPIInstance : IPublicAPI
    {
        private readonly SettingWindowViewModel _settingsVM;
        private readonly MainViewModel _mainVM;
        private readonly PinyinAlphabet _alphabet;

        #region Constructor

        public PublicAPIInstance(SettingWindowViewModel settingsVM, MainViewModel mainVM, PinyinAlphabet alphabet)
        {
            _settingsVM = settingsVM;
            _mainVM = mainVM;
            _alphabet = alphabet;
            GlobalHotkey.hookedKeyboardCallback = KListener_hookedKeyboardCallback;
            WebRequest.RegisterPrefix("data", new DataWebRequestFactory());
        }

        #endregion

        #region Public API

        public void ChangeQuery(string query, bool requery = false)
        {
            _mainVM.ChangeQueryText(query, requery);
        }

        public void RestartApp()
        {
            _mainVM.Hide();

            // we must manually save
            // UpdateManager.RestartApp() will call Environment.Exit(0)
            // which will cause ungraceful exit
            SaveAppAllSettings();

            // Restart requires Squirrel's Update.exe to be present in the parent folder, 
            // it is only published from the project's release pipeline. When debugging without it,
            // the project may not restart or just terminates. This is expected.
            UpdateManager.RestartApp(Constant.ApplicationFileName);
        }

        public void ShowMainWindow() => _mainVM.Show();

        public void HideMainWindow() => _mainVM.Hide();

        public bool IsMainWindowVisible() => _mainVM.MainWindowVisibilityStatus;

        public event VisibilityChangedEventHandler VisibilityChanged { add => _mainVM.VisibilityChanged += value; remove => _mainVM.VisibilityChanged -= value; }

        public void CheckForNewUpdate() => _settingsVM.UpdateApp();

        public void SaveAppAllSettings()
        {
            PluginManager.Save();
            _mainVM.Save();
            _settingsVM.Save();
            ImageLoader.Save();
        }

        public Task ReloadAllPluginData() => PluginManager.ReloadDataAsync();

        public void ShowMsgError(string title, string subTitle = "") =>
            ShowMsg(title, subTitle, Constant.ErrorIcon, true);

        public void ShowMsg(string title, string subTitle = "", string iconPath = "") =>
            ShowMsg(title, subTitle, iconPath, true);

        public void ShowMsg(string title, string subTitle, string iconPath, bool useMainWindowAsOwner = true)
        {
            Notification.Show(title, subTitle, iconPath);
        }

        public void OpenSettingDialog()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                SettingWindow sw = SingletonWindowOpener.Open<SettingWindow>(this, _settingsVM);
            });
        }

        public void ShellRun(string cmd, string filename = "cmd.exe")
        {
            var args = filename == "cmd.exe" ? $"/C {cmd}" : $"{cmd}";

            var startInfo = ShellCommand.SetProcessStartInfo(filename, arguments: args, createNoWindow: true);
            ShellCommand.Execute(startInfo);
        }

        public async void CopyToClipboard(string stringToCopy, bool directCopy = false, bool showDefaultNotification = true)
        {
            if (string.IsNullOrEmpty(stringToCopy))
                return;

            await Win32Helper.StartSTATaskAsync(() =>
            {
                var isFile = File.Exists(stringToCopy);
                if (directCopy && (isFile || Directory.Exists(stringToCopy)))
                {
                    var paths = new StringCollection
                    {
                        stringToCopy
                    };

                    Clipboard.SetFileDropList(paths);

                    if (showDefaultNotification)
                        ShowMsg(
                            $"{GetTranslation("copy")} {(isFile ? GetTranslation("fileTitle") : GetTranslation("folderTitle"))}",
                            GetTranslation("completedSuccessfully"));
                }
                else
                {
                    Clipboard.SetDataObject(stringToCopy);

                    if (showDefaultNotification)
                        ShowMsg(
                            $"{GetTranslation("copy")} {GetTranslation("textTitle")}",
                            GetTranslation("completedSuccessfully"));
                }
            });
        }

        public void StartLoadingBar() => _mainVM.ProgressBarVisibility = Visibility.Visible;

        public void StopLoadingBar() => _mainVM.ProgressBarVisibility = Visibility.Collapsed;

        public string GetTranslation(string key) => InternationalizationManager.Instance.GetTranslation(key);

        public List<PluginPair> GetAllPlugins() => PluginManager.AllPlugins.ToList();

        public MatchResult FuzzySearch(string query, string stringToCompare) =>
            StringMatcher.FuzzySearch(query, stringToCompare);

        public Task<string> HttpGetStringAsync(string url, CancellationToken token = default) => Http.GetAsync(url);

        public Task<Stream> HttpGetStreamAsync(string url, CancellationToken token = default) =>
            Http.GetStreamAsync(url);

        public Task HttpDownloadAsync([NotNull] string url, [NotNull] string filePath,
            CancellationToken token = default) => Http.DownloadAsync(url, filePath, token);

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

        public void LogException(string className, string message, Exception e,
            [CallerMemberName] string methodName = "") => Log.Exception(className, message, e, methodName);

        private readonly ConcurrentDictionary<Type, object> _pluginJsonStorages = new();

        /// <summary>
        /// Save plugin settings.
        /// </summary>
        public void SavePluginSettings()
        {
            foreach (var value in _pluginJsonStorages.Values)
            {
                var method = value.GetType().GetMethod("Save");
                method?.Invoke(value, null);
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

        public void SaveJsonStorage<T>(T settings) where T : new()
        {
            var type = typeof(T);
            _pluginJsonStorages[type] = new PluginJsonStorage<T>(settings);

            ((PluginJsonStorage<T>)_pluginJsonStorages[type]).Save();
        }

        public void OpenDirectory(string DirectoryPath, string FileNameOrFilePath = null)
        {
            using var explorer = new Process();
            var explorerInfo = _settingsVM.Settings.CustomExplorer;

            explorer.StartInfo = new ProcessStartInfo
            {
                FileName = explorerInfo.Path.Replace("%d", DirectoryPath),
                UseShellExecute = true,
                Arguments = FileNameOrFilePath is null
                    ? explorerInfo.DirectoryArgument.Replace("%d", DirectoryPath)
                    : explorerInfo.FileArgument
                        .Replace("%d", DirectoryPath)
                        .Replace("%f",
                            Path.IsPathRooted(FileNameOrFilePath) ? FileNameOrFilePath : Path.Combine(DirectoryPath, FileNameOrFilePath)
                        )
            };
            explorer.Start();
        }

        private void OpenUri(Uri uri, bool? inPrivate = null)
        {
            if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            {
                var browserInfo = _settingsVM.Settings.CustomBrowser;

                var path = browserInfo.Path == "*" ? "" : browserInfo.Path;

                if (browserInfo.OpenInTab)
                {
                    uri.AbsoluteUri.OpenInBrowserTab(path, inPrivate ?? browserInfo.EnablePrivate, browserInfo.PrivateArg);
                }
                else
                {
                    uri.AbsoluteUri.OpenInBrowserWindow(path, inPrivate ?? browserInfo.EnablePrivate, browserInfo.PrivateArg);
                }
            }
            else
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true
                })?.Dispose();

                return;
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

        public void RegisterGlobalKeyboardCallback(Func<int, int, SpecialKeyState, bool> callback) => _globalKeyboardHandlers.Add(callback);
        public void RemoveGlobalKeyboardCallback(Func<int, int, SpecialKeyState, bool> callback) => _globalKeyboardHandlers.Remove(callback);

        public void ReQuery(bool reselect = true) => _mainVM.ReQuery(reselect);

        public void BackToQueryResults() => _mainVM.BackToQueryResults();

        public MessageBoxResult ShowMsgBox(string messageBoxText, string caption = "", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.OK) =>
            MessageBoxEx.Show(messageBoxText, caption, button, icon, defaultResult);

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
