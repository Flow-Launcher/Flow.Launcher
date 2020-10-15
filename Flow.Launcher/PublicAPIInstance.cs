using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using Squirrel;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher
{
    public class PublicAPIInstance : IPublicAPI
    {
        private readonly SettingWindowViewModel _settingsVM;
        private readonly MainViewModel _mainVM;
        private readonly Alphabet _alphabet;

        #region Constructor

        public PublicAPIInstance(SettingWindowViewModel settingsVM, MainViewModel mainVM, Alphabet alphabet)
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

            UpdateManager.RestartApp(Constant.ApplicationFileName);
        }

        public void RestarApp()
        {
            RestartApp();
        }

        public void CheckForNewUpdate()
        {
            _settingsVM.UpdateApp();
        }

        public void SaveAppAllSettings()
        {
            _mainVM.Save();
            _settingsVM.Save();
            PluginManager.Save();
            ImageLoader.Save();
        }

        public void ReloadAllPluginData()
        {
            PluginManager.ReloadData();
        }

        public void ShowMsg(string title, string subTitle = "", string iconPath = "")
        {
            ShowMsg(title, subTitle, iconPath, true);
        }

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

        public void StartLoadingBar()
        {
            _mainVM.ProgressBarVisibility = Visibility.Visible;
        }

        public void StopLoadingBar()
        {
            _mainVM.ProgressBarVisibility = Visibility.Collapsed;
        }

        public void InstallPlugin(string path)
        {
            Application.Current.Dispatcher.Invoke(() => PluginManager.InstallPlugin(path));
        }

        public string GetTranslation(string key)
        {
            return InternationalizationManager.Instance.GetTranslation(key);
        }

        public List<PluginPair> GetAllPlugins()
        {
            return PluginManager.AllPlugins.ToList();
        }

        public event FlowLauncherGlobalKeyboardEventHandler GlobalKeyboardEvent;

        #endregion

        #region Private Methods

        private bool KListener_hookedKeyboardCallback(KeyEvent keyevent, int vkcode, SpecialKeyState state)
        {
            if (GlobalKeyboardEvent != null)
            {
                return GlobalKeyboardEvent((int)keyevent, vkcode, state);
            }
            return true;
        }
        #endregion
    }
}
