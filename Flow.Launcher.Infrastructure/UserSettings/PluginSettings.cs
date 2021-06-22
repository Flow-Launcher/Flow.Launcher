﻿using System.Collections.Generic;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public class PluginsSettings : BaseModel
    {
        public string PythonDirectory { get; set; }
        public Dictionary<string, Plugin> Plugins { get; set; } = new Dictionary<string, Plugin>();

        public void UpdatePluginSettings(List<PluginMetadata> metadatas)
        {
            foreach (var metadata in metadatas)
            {
                if (Plugins.ContainsKey(metadata.ID))
                {
                    var settings = Plugins[metadata.ID];

                    // TODO: Remove. This is backwards compatibility for 1.8.0 release.
                    // Introduced two new action keywords in Explorer, so need to update plugin setting in the UserData folder.
                    if (metadata.ID == "572be03c74c642baae319fc283e561a8" && metadata.ActionKeywords.Count != settings.ActionKeywords.Count)
                    {
                        settings.ActionKeywords.Add(Query.GlobalPluginWildcardSign); // for index search
                        settings.ActionKeywords.Add(Query.GlobalPluginWildcardSign); // for path search
                    }

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
                        Priority = metadata.Priority
                    };
                }
            }
        }
    }
    public class Plugin
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public List<string> ActionKeywords { get; set; } // a reference of the action keywords from plugin manager
        public int Priority { get; set; }

        /// <summary>
        /// Used only to save the state of the plugin in settings
        /// </summary>
        public bool Disabled { get; set; }
    }
}
