using Flow.Launcher.Plugin.SharedModels;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Public APIs that plugin can use
    /// </summary>
    public interface IPublicAPI
    {
        /// <summary>
        /// Change Flow.Launcher query
        /// </summary>
        /// <param name="query">query text</param>
        /// <param name="requery">
        /// force requery By default, Flow Launcher will not fire query if your query is same with existing one. 
        /// Set this to true to force Flow Launcher requerying
        /// </param>
        void ChangeQuery(string query, bool requery = false);

        /// <summary>
        /// Restart Flow Launcher
        /// </summary>
        void RestartApp();

        /// <summary>
        /// Save all Flow Launcher settings
        /// </summary>
        void SaveAppAllSettings();

        /// <summary>
        /// Reloads any Plugins that have the 
        /// IReloadable implemented. It refeshes
        /// Plugin's in memory data with new content
        /// added by user.
        /// </summary>
        Task ReloadAllPluginData();

        /// <summary>
        /// Check for new Flow Launcher update
        /// </summary>
        void CheckForNewUpdate();

        /// <summary>
        /// Show message box
        /// </summary>
        /// <param name="title">Message title</param>
        /// <param name="subTitle">Message subtitle</param>
        /// <param name="iconPath">Message icon path (relative path to your plugin folder)</param>
        void ShowMsg(string title, string subTitle = "", string iconPath = "");

        /// <summary>
        /// Show message box
        /// </summary>
        /// <param name="title">Message title</param>
        /// <param name="subTitle">Message subtitle</param>
        /// <param name="iconPath">Message icon path (relative path to your plugin folder)</param>
        /// <param name="useMainWindowAsOwner">when true will use main windows as the owner</param>
        void ShowMsg(string title, string subTitle, string iconPath, bool useMainWindowAsOwner = true);

        /// <summary>
        /// Open setting dialog
        /// </summary>
        void OpenSettingDialog();

        /// <summary>
        /// Get translation of current language
        /// You need to implement IPluginI18n if you want to support multiple languages for your plugin
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        string GetTranslation(string key);

        /// <summary>
        /// Get all loaded plugins 
        /// </summary>
        /// <returns></returns>
        List<PluginPair> GetAllPlugins();

        /// <summary>
        /// Fired after global keyboard events
        /// if you want to hook something like Ctrl+R, you should use this event
        /// </summary>
        event FlowLauncherGlobalKeyboardEventHandler GlobalKeyboardEvent;

        /// <summary>
        /// Fuzzy Search the string with query
        /// </summary>
        /// <param name="query">Query String</param>
        /// <param name="stringToCompare">The string to Search for Query</param>
        /// <returns>Match results</returns>
        MatchResult FuzzySearch(string query, string stringToCompare);

        /// <summary>
        /// Http Get to the spefic URL
        /// </summary>
        /// <param name="url">URL to call Http Get</param>
        /// <param name="token">Cancellation Token</param>
        /// <returns>Task to get string result</returns>
        Task<string> HttpGetStringAsync(string url, CancellationToken token = default);

        /// <summary>
        /// Http Get to the spefic URL
        /// </summary>
        /// <param name="url">URL to call Http Get</param>
        /// <param name="token">Cancellation Token</param>
        /// <returns>Task to get stream result</returns>
        Task<Stream> HttpGetStreamAsync(string url, CancellationToken token = default);

        /// <summary>
        /// Download the specific url to a cretain file path
        /// </summary>
        /// <param name="url">URL to download file</param>
        /// <param name="token">place to store file</param>
        /// <returns>Task showing the progress</returns>
        Task HttpDownloadAsync([NotNull] string url, [NotNull] string filePath, CancellationToken token = default);

        /// <summary>
        /// Add ActionKeyword for specific plugin
        /// </summary>
        /// <param name="pluginId">ID for plugin that needs to add action keyword</param>
        /// <param name="newActionKeyword">The actionkeyword that is supposed to be added</param>
        void AddActionKeyword(string pluginId, string newActionKeyword);

        /// <summary>
        /// Remove ActionKeyword for specific plugin
        /// </summary>
        /// <param name="pluginId">ID for plugin that needs to remove action keyword</param>
        /// <param name="newActionKeyword">The actionkeyword that is supposed to be removed</param>
        void RemoveActionKeyword(string pluginId, string oldActionKeyword);

        /// <summary>
        /// Log Debug message
        /// Message will only be logged in Debug mode
        /// </summary>
        void LogDebug(string className, string message, [CallerMemberName] string methodName = "");

        /// <summary>
        /// Log Message
        /// </summary>
        void LogInfo(string className, string message, [CallerMemberName] string methodName = "");

        /// <summary>
        /// Log Warning
        /// </summary>
        void LogWarn(string className, string message, [CallerMemberName] string methodName = "");

        /// <summary>
        /// Log an Exception
        /// </summary>
        void LogException(string className, string message, Exception e, [CallerMemberName] string methodName = "");

        /// <summary>
        /// Load JsonStorage for current plugin
        /// </summary>
        /// <typeparam name="T">Type for deserialization</typeparam>
        /// <returns></returns>
        T LoadJsonStorage<T>() where T : new();

        /// <summary>
        /// Save JsonStorage for current plugin
        /// </summary>
        /// <typeparam name="T">Type for Serialization</typeparam>
        /// <returns></returns>
        void SaveJsonStorage<T>(T setting) where T : new();
    }
}
