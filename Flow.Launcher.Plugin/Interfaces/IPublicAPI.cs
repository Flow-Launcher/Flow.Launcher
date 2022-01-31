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
        /// Run a shell command
        /// </summary>
        /// <param name="cmd">The command or program to run</param>
        /// <param name="filename">the shell type to run, e.g. powershell.exe</param>
        /// <exception cref="FileNotFoundException">Thrown when unable to find the file specified in the command </exception>
        /// <exception cref="Win32Exception">Thrown when error occurs during the execution of the command </exception>
        void ShellRun(string cmd, string filename = "cmd.exe");
        
        /// <summary>
        /// Copy Text to clipboard
        /// </summary>
        /// <param name="Text">Text to save on clipboard</param>
        public void CopyToClipboard(string text);

        /// <summary>
        /// Save everything, all of Flow Launcher and plugins' data and settings
        /// </summary>
        void SaveAppAllSettings();

        /// <summary>
        /// Save all Flow's plugins settings
        /// </summary>
        void SavePluginSettings();

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
        /// Show the error message using Flow's standard error icon.
        /// </summary>
        /// <param name="title">Message title</param>
        /// <param name="subTitle">Optional message subtitle</param>
        void ShowMsgError(string title, string subTitle = "");

        /// <summary>
        /// Show the MainWindow when hiding
        /// </summary>
        void ShowMainWindow();

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
        [Obsolete("Unable to Retrieve correct return value")]
        event FlowLauncherGlobalKeyboardEventHandler GlobalKeyboardEvent;
        
        /// <summary>
        /// Register a callback for Global Keyboard Event
        /// </summary>
        /// <param name="callback"></param>
        public void RegisterGlobalKeyboardCallback(Func<int, int, SpecialKeyState, bool> callback);
        
        /// <summary>
        /// Remove a callback for Global Keyboard Event
        /// </summary>
        /// <param name="callback"></param>
        public void RemoveGlobalKeyboardCallback(Func<int, int, SpecialKeyState, bool> callback);


        /// <summary>
        /// Fuzzy Search the string with the given query. This is the core search mechanism Flow uses
        /// </summary>
        /// <param name="query">Query string</param>
        /// <param name="stringToCompare">The string that will be compared against the query</param>
        /// <returns>Match results</returns>
        MatchResult FuzzySearch(string query, string stringToCompare);

        /// <summary>
        /// Http download the spefic url and return as string
        /// </summary>
        /// <param name="url">URL to call Http Get</param>
        /// <param name="token">Cancellation Token</param>
        /// <returns>Task to get string result</returns>
        Task<string> HttpGetStringAsync(string url, CancellationToken token = default);

        /// <summary>
        /// Http download the spefic url and return as stream
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
        /// Log debug message
        /// Message will only be logged in Debug mode
        /// </summary>
        void LogDebug(string className, string message, [CallerMemberName] string methodName = "");

        /// <summary>
        /// Log info message
        /// </summary>
        void LogInfo(string className, string message, [CallerMemberName] string methodName = "");

        /// <summary>
        /// Log warning message
        /// </summary>
        void LogWarn(string className, string message, [CallerMemberName] string methodName = "");

        /// <summary>
        /// Log an Exception. Will throw if in debug mode so developer will be aware, 
        /// otherwise logs the eror message. This is the primary logging method used for Flow 
        /// </summary>
        void LogException(string className, string message, Exception e, [CallerMemberName] string methodName = "");

        /// <summary>
        /// Load JsonStorage for current plugin's setting. This is the method used to load settings from json in Flow.
        /// When the file is not exist, it will create a new instance for the specific type.
        /// </summary>
        /// <typeparam name="T">Type for deserialization</typeparam>
        /// <returns></returns>
        T LoadSettingJsonStorage<T>() where T : new();

        /// <summary>
        /// Save JsonStorage for current plugin's setting. This is the method used to save settings to json in Flow.Launcher
        /// This method will save the original instance loaded with LoadJsonStorage.
        /// This API call is for manually Save. Flow will automatically save all setting type that has called LoadSettingJsonStorage or SaveSettingJsonStorage previously.
        /// </summary>
        /// <typeparam name="T">Type for Serialization</typeparam>
        /// <returns></returns>
        void SaveSettingJsonStorage<T>() where T : new();

        /// <summary>
        /// Open directory in an explorer configured by user via Flow's Settings. The default is Windows Explorer
        /// </summary>
        /// <param name="DirectoryPath">Directory Path to open</param>
        /// <param name="FileName">Extra FileName Info</param>
        public void OpenDirectory(string DirectoryPath, string FileName = null);

        /// <summary>
        /// Opens the URL. The browser and mode used is based on what's configured in Flow's default browser settings.
        /// Also supports application URIs e.g. obsidian://search-query-example
        /// </summary>
        public void OpenUrl(string url, bool? inPrivate = null);

        /// <summary>
        /// Opens the application URI, e.g. obsidian://search-query-example
        /// </summary>
        public void OpenAppUri(string appUri);
    }
}
