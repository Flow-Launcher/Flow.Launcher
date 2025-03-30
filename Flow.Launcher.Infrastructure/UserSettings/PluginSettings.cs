using System.Collections.Generic;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public class PluginsSettings : BaseModel
    {
        private string pythonExecutablePath = string.Empty;
        public string PythonExecutablePath
        {
            get => pythonExecutablePath;
            set
            {
                pythonExecutablePath = value;
                Constant.PythonPath = value;
            }
        }

        private string nodeExecutablePath = string.Empty;
        public string NodeExecutablePath
        {
            get => nodeExecutablePath;
            set 
            {
                nodeExecutablePath = value;
                Constant.NodePath = value;
            }
        }

        private Dictionary<string, Plugin> Plugins { get; set; } = new();

        public void UpdatePluginSettings(List<PluginMetadata> metadatas)
        {
            foreach (var metadata in metadatas)
            {
                if (Plugins.TryGetValue(metadata.ID, out var settings))
                {
                    if (string.IsNullOrEmpty(settings.Version))
                        settings.Version = metadata.Version;

                    if (settings.ActionKeywords?.Count > 0)
                    {
                        metadata.ActionKeywords = settings.ActionKeywords;
                        metadata.ActionKeyword = settings.ActionKeywords[0];
                    }
                    else
                    {
                        metadata.ActionKeywords = new List<string>();
                        metadata.ActionKeyword = string.Empty;
                    }
                    metadata.Disabled = settings.Disabled;
                    metadata.Priority = settings.Priority;
                    metadata.SearchDelay = settings.SearchDelay;
                }
                else
                {
                    Plugins[metadata.ID] = new Plugin
                    {
                        ID = metadata.ID,
                        Name = metadata.Name,
                        Version = metadata.Version,
                        ActionKeywords = metadata.ActionKeywords,
                        Disabled = metadata.Disabled,
                        Priority = metadata.Priority,
                        SearchDelay = metadata.SearchDelay,
                    };
                }
            }
        }

        public Plugin GetPluginSettings(string id)
        {
            if (Plugins.TryGetValue(id, out var plugin))
            {
                return plugin;
    }
            return null;
        }

        public Plugin RemovePluginSettings(string id)
        {
            Plugins.Remove(id, out var plugin);
            return plugin;
        }
    }

    public class Plugin
    {
        public string ID { get; set; }

        public string Name { get; set; }

        public string Version { get; set; }
        public List<string> ActionKeywords { get; set; } // a reference of the action keywords from plugin manager
        public int Priority { get; set; }
        public int SearchDelay { get; set; }

        /// <summary>
        /// Used only to save the state of the plugin in settings
        /// </summary>
        public bool Disabled { get; set; }
    }
}
