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

        /// <summary>
        /// Change Flow.Launcher query
        /// </summary>
        /// <param name="query">query text</param>
        /// <param name="requery">
        /// Force requery. By default, Flow Launcher will not fire query if your query is same with existing one. 
        /// Set this to <see langword="true"/> to force Flow Launcher requerying
        /// </param>
        public void ChangeQuery(string query, bool requery = false)
        {
            _api.ChangeQuery(query, requery);
        }

        /// <summary>
        /// Restart Flow Launcher
        /// </summary>
        public void RestartApp()
        {
            _api.RestartApp();
        }

        /// <summary>
        /// Run a shell command
        /// </summary>
        /// <param name="cmd">The command or program to run</param>
        /// <param name="filename">the shell type to run, e.g. powershell.exe</param>
        /// <exception cref="FileNotFoundException">Thrown when unable to find the file specified in the command </exception>
        /// <exception cref="Win32Exception">Thrown when error occurs during the execution of the command </exception>
        public void ShellRun(string cmd, string filename = "cmd.exe")
        {
            _api.ShellRun(cmd, filename);
        }

        /// <summary>
        /// Copies the passed in text and shows a message indicating whether the operation was completed successfully.
        /// When directCopy is set to true and passed in text is the path to a file or directory,
        /// the actual file/directory will be copied to clipboard. Otherwise the text itself will still be copied to clipboard.
        /// </summary>
        /// <param name="text">Text to save on clipboard</param>
        /// <param name="directCopy">When true it will directly copy the file/folder from the path specified in text</param>
        /// <param name="showDefaultNotification">Whether to show the default notification from this method after copy is done. 
        ///                                         It will show file/folder/text is copied successfully.
        ///                                         Turn this off to show your own notification after copy is done.</param>>
        public void CopyToClipboard(string text, bool directCopy = false, bool showDefaultNotification = true)
        {
            _api.CopyToClipboard(text, directCopy, showDefaultNotification);
        }

        /// <summary>
        /// Save everything, all of Flow Launcher and plugins' data and settings
        /// </summary>
        public void SaveAppAllSettings()
        {
            _api.SaveAppAllSettings();
        }

        /// <summary>
        /// Save all Flow's plugins settings
        /// </summary>
        public void SavePluginSettings()
        {
            _api.SavePluginSettings();
        }

        /// <summary>
        /// Reloads any Plugins that have the 
        /// IReloadable implemented. It refeshes
        /// Plugin's in memory data with new content
        /// added by user.
        /// </summary>
        public Task ReloadAllPluginDataAsync()
        {
            return _api.ReloadAllPluginData();
        }

        /// <summary>
        /// Check for new Flow Launcher update
        /// </summary>
        public void CheckForNewUpdate()
        {
            _api.CheckForNewUpdate();
        }

        /// <summary>
        /// Show the error message using Flow's standard error icon.
        /// </summary>
        /// <param name="title">Message title</param>
        /// <param name="subTitle">Optional message subtitle</param>
        public void ShowMsgError(string title, string subTitle = "")
        {
            _api.ShowMsgError(title, subTitle);
        }

        /// <summary>
        /// Show the MainWindow when hiding
        /// </summary>
        public void ShowMainWindow()
        {
            _api.ShowMainWindow();
        }

        /// <summary>
        /// Hide MainWindow
        /// </summary>
        public void HideMainWindow()
        {
            _api.HideMainWindow();
        }

        /// <summary>
        /// Representing whether the main window is visible
        /// </summary>
        /// <returns></returns>
        public bool IsMainWindowVisible()
        {
            return _api.IsMainWindowVisible();
        }

        /// <summary>
        /// Show message box
        /// </summary>
        /// <param name="title">Message title</param>
        /// <param name="subTitle">Message subtitle</param>
        /// <param name="iconPath">Message icon path (relative path to your plugin folder)</param>
        public void ShowMsg(string title, string subTitle = "", string iconPath = "")
        {
            _api.ShowMsg(title, subTitle, iconPath);
        }

        /// <summary>
        /// Show message box
        /// </summary>
        /// <param name="title">Message title</param>
        /// <param name="subTitle">Message subtitle</param>
        /// <param name="iconPath">Message icon path (relative path to your plugin folder)</param>
        /// <param name="useMainWindowAsOwner">when true will use main windows as the owner</param>
        public void ShowMsg(string title, string subTitle, string iconPath, bool useMainWindowAsOwner = true)
        {
            _api.ShowMsg(title, subTitle, iconPath, useMainWindowAsOwner);
        }

        /// <summary>
        /// Open setting dialog
        /// </summary>
        public void OpenSettingDialog()
        {
            _api.OpenSettingDialog();
        }

        /// <summary>
        /// Get translation of current language
        /// You need to implement IPluginI18n if you want to support multiple languages for your plugin
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetTranslation(string key)
        {
            return _api.GetTranslation(key);
        }

        /// <summary>
        /// Get all loaded plugins 
        /// </summary>
        /// <returns></returns>
        public List<PluginPair> GetAllPlugins()
        {
            return _api.GetAllPlugins();
        }


        /// <summary>
        /// Fuzzy Search the string with the given query. This is the core search mechanism Flow uses
        /// </summary>
        /// <param name="query">Query string</param>
        /// <param name="stringToCompare">The string that will be compared against the query</param>
        /// <returns>Match results</returns>
        public MatchResult FuzzySearch(string query, string stringToCompare)
        {
            return _api.FuzzySearch(query, stringToCompare);
        }

        /// <summary>
        /// Http download the spefic url and return as string
        /// </summary>
        /// <param name="url">URL to call Http Get</param>
        /// <param name="token">Cancellation Token</param>
        /// <returns>Task to get string result</returns>
        public Task<string> HttpGetStringAsync(string url, CancellationToken token = default)
        {
            return _api.HttpGetStringAsync(url, token);
        }

        /// <summary>
        /// Http download the spefic url and return as stream
        /// </summary>
        /// <param name="url">URL to call Http Get</param>
        /// <param name="token">Cancellation Token</param>
        /// <returns>Task to get stream result</returns>
        public Task<Stream> HttpGetStreamAsync(string url, CancellationToken token = default)
        {
            return _api.HttpGetStreamAsync(url, token);
        }

        /// <summary>
        /// Download the specific url to a cretain file path
        /// </summary>
        /// <param name="url">URL to download file</param>
        /// <param name="filePath">path to save downloaded file</param>
        /// <param name="token">place to store file</param>
        /// <returns>Task showing the progress</returns>
        public Task HttpDownloadAsync([NotNull] string url, [NotNull] string filePath,
            CancellationToken token = default)
        {
            return _api.HttpDownloadAsync(url, filePath, token);
        }

        /// <summary>
        /// Add ActionKeyword for specific plugin
        /// </summary>
        /// <param name="pluginId">ID for plugin that needs to add action keyword</param>
        /// <param name="newActionKeyword">The actionkeyword that is supposed to be added</param>
        public void AddActionKeyword(string pluginId, string newActionKeyword)
        {
            _api.AddActionKeyword(pluginId, newActionKeyword);
        }

        /// <summary>
        /// Remove ActionKeyword for specific plugin
        /// </summary>
        /// <param name="pluginId">ID for plugin that needs to remove action keyword</param>
        /// <param name="oldActionKeyword">The actionkeyword that is supposed to be removed</param>
        public void RemoveActionKeyword(string pluginId, string oldActionKeyword)
        {
            _api.RemoveActionKeyword(pluginId, oldActionKeyword);
        }

        /// <summary>
        /// Check whether specific ActionKeyword is assigned to any of the plugin
        /// </summary>
        /// <param name="actionKeyword">The actionkeyword for checking</param>
        /// <returns>True if the actionkeyword is already assigned, False otherwise</returns>
        public bool ActionKeywordAssigned(string actionKeyword)
        {
            return _api.ActionKeywordAssigned(actionKeyword);
        }

        /// <summary>
        /// Log debug message
        /// Message will only be logged in Debug mode
        /// </summary>
        public void LogDebug(string className, string message, [CallerMemberName] string methodName = "")
        {
            _api.LogDebug(className, message, methodName);
        }

        /// <summary>
        /// Log info message
        /// </summary>
        public void LogInfo(string className, string message, [CallerMemberName] string methodName = "")
        {
            _api.LogInfo(className, message, methodName);
        }

        /// <summary>
        /// Log warning message
        /// </summary>
        public void LogWarn(string className, string message, [CallerMemberName] string methodName = "")
        {
            _api.LogWarn(className, message, methodName);
        }


        /// <summary>
        /// Open directory in an explorer configured by user via Flow's Settings. The default is Windows Explorer
        /// </summary>
        /// <param name="DirectoryPath">Directory Path to open</param>
        /// <param name="FileNameOrFilePath">Extra FileName Info</param>
        public void OpenDirectory(string DirectoryPath, string FileNameOrFilePath = null)
        {
            _api.OpenDirectory(DirectoryPath, FileNameOrFilePath);
        }


        /// <summary>
        /// Opens the URL with the given string. 
        /// The browser and mode used is based on what's configured in Flow's default browser settings.
        /// Non-C# plugins should use this method.
        /// </summary>
        public void OpenUrl(string url, bool? inPrivate = null)
        {
            _api.OpenUrl(url, inPrivate);
        }


        /// <summary>
        /// Opens the application URI with the given string, e.g. obsidian://search-query-example
        /// Non-C# plugins should use this method
        /// </summary>
        public void OpenAppUri(string appUri)
        {
            _api.OpenAppUri(appUri);
        }
    }
}
