using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.Core.Plugin.JsonRPCV2Models
{
    public class JsonRPCPublicAPI
    {
        private IPublicAPI _api;

        public JsonRPCPublicAPI(IPublicAPI api)
        {
            _api = api;
        }

        public void ChangeQuery(string query, bool requery = false)
        {
            _api.ChangeQuery(query, requery);
        }

        public void RestartApp()
        {
            _api.RestartApp();
        }

        public void ShellRun(string cmd, string filename = "cmd.exe")
        {
            _api.ShellRun(cmd, filename);
        }

        public void CopyToClipboard(string text, bool directCopy = false, bool showDefaultNotification = true)
        {
            _api.CopyToClipboard(text, directCopy, showDefaultNotification);
        }

        public void SaveAppAllSettings()
        {
            _api.SaveAppAllSettings();
        }

        public void SavePluginSettings()
        {
            _api.SavePluginSettings();
        }

        public Task ReloadAllPluginDataAsync()
        {
            return _api.ReloadAllPluginData();
        }

        public void CheckForNewUpdate()
        {
            _api.CheckForNewUpdate();
        }

        public void ShowMsgError(string title, string subTitle = "")
        {
            _api.ShowMsgError(title, subTitle);
        }

        public void ShowMainWindow()
        {
            _api.ShowMainWindow();
        }

        public void HideMainWindow()
        {
            _api.HideMainWindow();
        }

        public bool IsMainWindowVisible()
        {
            return _api.IsMainWindowVisible();
        }

        public void ShowMsg(string title, string subTitle = "", string iconPath = "")
        {
            _api.ShowMsg(title, subTitle, iconPath);
        }

        public void ShowMsg(string title, string subTitle, string iconPath, bool useMainWindowAsOwner = true)
        {
            _api.ShowMsg(title, subTitle, iconPath, useMainWindowAsOwner);
        }

        public void OpenSettingDialog()
        {
            _api.OpenSettingDialog();
        }

        public string GetTranslation(string key)
        {
            return _api.GetTranslation(key);
        }

        public List<PluginPair> GetAllPlugins()
        {
            return _api.GetAllPlugins();
        }


        public MatchResult FuzzySearch(string query, string stringToCompare)
        {
            return _api.FuzzySearch(query, stringToCompare);
        }

        public Task<string> HttpGetStringAsync(string url, CancellationToken token = default)
        {
            return _api.HttpGetStringAsync(url, token);
        }

        public Task<Stream> HttpGetStreamAsync(string url, CancellationToken token = default)
        {
            return _api.HttpGetStreamAsync(url, token);
        }

        public Task HttpDownloadAsync([NotNull] string url, [NotNull] string filePath,
            CancellationToken token = default)
        {
            return _api.HttpDownloadAsync(url, filePath, token);
        }

        public void AddActionKeyword(string pluginId, string newActionKeyword)
        {
            _api.AddActionKeyword(pluginId, newActionKeyword);
        }

        public void RemoveActionKeyword(string pluginId, string oldActionKeyword)
        {
            _api.RemoveActionKeyword(pluginId, oldActionKeyword);
        }

        public bool ActionKeywordAssigned(string actionKeyword)
        {
            return _api.ActionKeywordAssigned(actionKeyword);
        }

        public void LogDebug(string className, string message, [CallerMemberName] string methodName = "")
        {
            _api.LogDebug(className, message, methodName);
        }

        public void LogInfo(string className, string message, [CallerMemberName] string methodName = "")
        {
            _api.LogInfo(className, message, methodName);
        }

        public void LogWarn(string className, string message, [CallerMemberName] string methodName = "")
        {
            _api.LogWarn(className, message, methodName);
        }

        public void OpenDirectory(string DirectoryPath, string FileNameOrFilePath = null)
        {
            _api.OpenDirectory(DirectoryPath, FileNameOrFilePath);
        }


        public void OpenUrl(string url, bool? inPrivate = null)
        {
            _api.OpenUrl(url, inPrivate);
        }


        public void OpenAppUri(string appUri)
        {
            _api.OpenAppUri(appUri);
        }
    }
}
