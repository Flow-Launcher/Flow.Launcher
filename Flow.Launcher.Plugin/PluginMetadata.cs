using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin
{
    public class PluginMetadata : BaseModel
    {
        private string _pluginDirectory;
        /// <summary>
        /// Unique ID of the plugin
        /// </summary>
        public string ID { get; set; }
        /// <summary>
        /// Name of the plugin
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Author of the plugin
        /// </summary>
        public string Author { get; set; }
        /// <summary>
        /// Plugin Version
        /// </summary>
        public string Version { get; set; }
        /// <summary>
        /// Programming Language of the plugin
        /// </summary>
        public string Language { get; set; }
        /// <summary>
        /// Description of the plugin
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// Website of the plugin
        /// </summary>
        public string Website { get; set; }
        /// <summary>
        /// Whether the plugin is enabled
        /// </summary>
        public bool Disabled { get; set; }
        /// <summary>
        /// Executable file path of the plugin
        /// </summary>
        public string ExecuteFilePath { get; private set;}

        /// <summary>
        /// Executable file Name of the plugin
        /// </summary>
        public string ExecuteFileName { get; set; }

        public string PluginDirectory
        {
            get { return _pluginDirectory; }
            internal set
            {
                _pluginDirectory = value;
                ExecuteFilePath = Path.Combine(value, ExecuteFileName);
                IcoPath = Path.Combine(value, IcoPath);
            }
        }

        /// <summary>
        /// Action keyword of the plugin (Obsolete)
        /// </summary>
        public string ActionKeyword { get; set; }

        /// <summary>
        /// Action keywords of the plugin
        /// </summary>
        public List<string> ActionKeywords { get; set; }

        /// <summary>
        /// Icon path of the plugin
        /// </summary>
        public string IcoPath { get; set;}
        
        /// <summary>
        /// Metadata ToString
        /// </summary>
        /// <returns>Full Name of Plugin</returns>
        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        /// Plugin Priority
        /// </summary>
        [JsonIgnore]
        public int Priority { get; set; }

        /// <summary>
        /// Init time include both plugin load time and init time
        /// </summary>
        [JsonIgnore]
        public long InitTime { get; set; }
        /// <summary>
        /// Plugin Average Query Time (Statistics)
        /// </summary>
        [JsonIgnore]
        public long AvgQueryTime { get; set; }
        /// <summary>
        /// Plugin Query Count (Statistics)
        /// </summary>
        [JsonIgnore]
        public int QueryCount { get; set; }
    }
}
