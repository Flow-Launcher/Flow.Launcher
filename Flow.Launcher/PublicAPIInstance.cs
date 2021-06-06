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
using System.Threading;
using System.IO;
using Flow.Launcher.Infrastructure.Http;
using JetBrains.Annotations;
using System.Runtime.CompilerServices;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;
using System.Collections.Concurrent;

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
            GlobalHotkey.Instance.hookedKeyboardCallback += KListener_hookedKeyboardCallback;
            WebRequest.RegisterPrefix("data", new DataWebRequestFactory());
        }

        #endregion

        #region Public API

        public void ChangeQuery(string query, bool requery = false)
        {
            _mainVM.ChangeQueryText(query);
        }

        public void ChangeQueryText(string query, bool selectAll = false)
        {
            _mainVM.ChangeQueryText(query);
        }

        public void RestartApp()
        {
            _mainVM.MainWindowVisibility = Visibility.Hidden;

            // we must manually save
            // UpdateManager.RestartApp() will call Environment.Exit(0)
            // which will cause ungraceful exit
            SaveAppAllSettings();

            // Restart requires Squirrel's Update.exe to be present in the parent folder, 
            // it is only published from the project's release pipeline. When debugging without it,
            // the project may not restart or just terminates. This is expected.
            UpdateManager.RestartApp(Constant.ApplicationFileName);
        }

        public void RestarApp() => RestartApp();

        public void CheckForNewUpdate() => _settingsVM.UpdateApp();

        public void SaveAppAllSettings()
        {
            SavePluginSettings();
            _mainVM.Save();
            _settingsVM.Save();
            ImageLoader.Save();
        }

        public Task ReloadAllPluginData() => PluginManager.ReloadData();

        public void ShowMsgError(string title, string subTitle = "") =>
            ShowMsg(title, subTitle, Constant.ErrorIcon, true);

        public void ShowMsg(string title, string subTitle = "", string iconPath = "") =>
            ShowMsg(title, subTitle, iconPath, true);

        public void ShowMsg(string title, string subTitle, string iconPath, bool useMainWindowAsOwner = true)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var msg = useMainWindowAsOwner ? new Msg {Owner = Application.Current.MainWindow} : new Msg();
                msg.Show(title, subTitle, iconPath);
            });
        }

        public void OpenSettingDialog()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                SettingWindow sw = SingletonWindowOpener.Open<SettingWindow>(this, _settingsVM);
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

            return ((PluginJsonStorage<T>) _pluginJsonStorages[type]).Load();
        }

        public void SaveSettingJsonStorage<T>() where T : new()
        {
            var type = typeof(T);
            if (!_pluginJsonStorages.ContainsKey(type))
                _pluginJsonStorages[type] = new PluginJsonStorage<T>();

            ((PluginJsonStorage<T>) _pluginJsonStorages[type]).Save();
        }

        public void SaveJsonStorage<T>(T settings) where T : new()
        {
            var type = typeof(T);
            _pluginJsonStorages[type] = new PluginJsonStorage<T>(settings);

            ((PluginJsonStorage<T>) _pluginJsonStorages[type]).Save();
        }

        public event FlowLauncherGlobalKeyboardEventHandler GlobalKeyboardEvent;

        #endregion

        #region Private Methods

        private bool KListener_hookedKeyboardCallback(KeyEvent keyevent, int vkcode, SpecialKeyState state)
        {
            if (GlobalKeyboardEvent != null)
            {
                return GlobalKeyboardEvent((int) keyevent, vkcode, state);
            }

            return true;
        }

        #endregion
    }
}