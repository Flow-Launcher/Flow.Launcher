using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Plugin metadata
    /// </summary>
    public class PluginMetadata : BaseModel
    {
        /// <summary>
        /// Plugin ID.
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// Plugin name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Plugin author.
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Plugin version.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Plugin language.
        /// See <see cref="AllowedLanguage"/>
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Plugin description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Plugin website.
        /// </summary>
        public string Website { get; set; }

        /// <summary>
        /// Whether plugin is disabled.
        /// </summary>
        public bool Disabled { get; set; }

        /// <summary>
        /// Whether plugin is disabled in home query.
        /// </summary>
        public bool HomeDisabled { get; set; }

        /// <summary>
        /// Plugin execute file path.
        /// </summary>
        public string ExecuteFilePath { get; private set; }

        /// <summary>
        /// Plugin execute file name.
        /// </summary>
        public string ExecuteFileName { get; set; }

        /// <summary>
        /// Plugin assembly name.
        /// Only available for .Net plugins.
        /// </summary>
        [JsonIgnore]
        public string AssemblyName { get; internal set; }

        private string _pluginDirectory;

        /// <summary>
        /// Plugin source directory.
        /// </summary>
        public string PluginDirectory
        {
            get => _pluginDirectory;
            internal set
            {
                _pluginDirectory = value;
                ExecuteFilePath = Path.Combine(value, ExecuteFileName);
                IcoPath = Path.Combine(value, IcoPath);
            }
        }

        /// <summary>
        /// The first action keyword of plugin.
        /// </summary>
        public string ActionKeyword { get; set; }

        /// <summary>
        /// All action keywords of plugin.
        /// </summary>
        public List<string> ActionKeywords { get; set; }

        /// <summary>
        /// Hide plugin keyword setting panel.
        /// </summary>
        public bool HideActionKeywordPanel { get; set; }

        /// <summary>
        /// Plugin search delay time in ms. Null means use default search delay time.
        /// </summary>
        public int? SearchDelayTime { get; set; } = null;

        /// <summary>
        /// Plugin icon path.
        /// </summary>
        public string IcoPath { get; set;}

        /// <summary>
        /// Plugin priority.
        /// </summary>
        [JsonIgnore]
        public int Priority { get; set; }

        /// <summary>
        /// Init time include both plugin load time and init time.
        /// </summary>
        [JsonIgnore]
        public long InitTime { get; set; }

        /// <summary>
        /// Average query time.
        /// </summary>
        [JsonIgnore]
        public long AvgQueryTime { get; set; }

        /// <summary>
        /// Query count.
        /// </summary>
        [JsonIgnore]
        public int QueryCount { get; set; }

        /// <summary>
        /// The path to the plugin settings directory which is not validated.
        /// It is used to store plugin settings files and data files.
        /// When plugin is deleted, FL will ask users whether to keep its settings.
        /// If users do not want to keep, this directory will be deleted.
        /// </summary>
        public string PluginSettingsDirectoryPath { get; internal set; }

        /// <summary>
        /// The path to the plugin cache directory which is not validated.
        /// It is used to store cache files.
        /// When plugin is deleted, this directory will be deleted as well.
        /// </summary>
        public string PluginCacheDirectoryPath { get; internal set; }

        /// <summary>
        /// Convert <see cref="PluginMetadata"/> to string.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Name;
        }
    }
}
