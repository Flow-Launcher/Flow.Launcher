using System.Collections.Generic;
using System.Text.Json.Serialization;
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

        /// <summary>
        /// Only used for serialization
        /// </summary>
        public Dictionary<string, Plugin> Plugins { get; set; } = new();

        /// <summary>
        /// Update plugin settings with metadata.
        /// FL will get default values from metadata first and then load settings to metadata
        /// </summary>
        /// <param name="metadatas">Parsed plugin metadatas</param>
        public void UpdatePluginSettings(List<PluginMetadata> metadatas)
        {
            foreach (var metadata in metadatas)
            {
                if (Plugins.TryGetValue(metadata.ID, out var settings))
                {
                    // If settings exist, update settings & metadata value
                    // update settings values with metadata
                    if (string.IsNullOrEmpty(settings.Version))
                    {
                        settings.Version = metadata.Version;
                    }
                    settings.DefaultActionKeywords = metadata.ActionKeywords; // metadata provides default values
                    settings.DefaultSearchDelayTime = metadata.SearchDelayTime; // metadata provides default values

                    // update metadata values with settings
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
                    metadata.SearchDelayTime = settings.SearchDelayTime;
                    metadata.HomeDisabled = settings.HomeDisabled;
                }
                else
                {
                    // If settings does not exist, create a new one
                    Plugins[metadata.ID] = new Plugin
                    {
                        ID = metadata.ID,
                        Name = metadata.Name,
                        Version = metadata.Version,
                        DefaultActionKeywords = metadata.ActionKeywords, // metadata provides default values
                        ActionKeywords = metadata.ActionKeywords, // use default value
                        Disabled = metadata.Disabled,
                        HomeDisabled = metadata.HomeDisabled,
                        Priority = metadata.Priority,
                        DefaultSearchDelayTime = metadata.SearchDelayTime, // metadata provides default values
                        SearchDelayTime = metadata.SearchDelayTime, // use default value
                    };
                }
            }
        }

        /// <summary>
        /// Update plugin hotkey information in metadata and plugin setting.
        /// </summary>
        /// <param name="hotkeyPluginInfo"></param>
        public void UpdatePluginHotkeyInfo(IDictionary<PluginPair, List<BasePluginHotkey>> hotkeyPluginInfo)
        {
            foreach (var info in hotkeyPluginInfo)
            {
                var pluginPair = info.Key;
                var hotkeyInfo = info.Value;
                var metadata = pluginPair.Metadata;
                if (Plugins.TryGetValue(pluginPair.Metadata.ID, out var plugin))
                {
                    if (plugin.pluginHotkeys == null || plugin.pluginHotkeys.Count == 0)
                    {
                        // If plugin hotkeys does not exist, create a new one and initialize with default values
                        plugin.pluginHotkeys = new List<PluginHotkey>();
                        foreach (var hotkey in hotkeyInfo)
                        {
                            plugin.pluginHotkeys.Add(new PluginHotkey
                            {
                                Id = hotkey.Id,
                                DefaultHotkey = hotkey.DefaultHotkey, // hotkey info provides default values
                                Hotkey = hotkey.DefaultHotkey // use default value
                            });
                            metadata.PluginHotkeys.Add(new PluginHotkey
                            {
                                Id = hotkey.Id,
                                DefaultHotkey = hotkey.DefaultHotkey, // hotkey info provides default values
                                Hotkey = hotkey.DefaultHotkey // use default value
                            });
                        }
                    }
                    else
                    {
                        // If plugin hotkeys exist, update the existing hotkeys with the new values
                        foreach (var hotkey in hotkeyInfo)
                        {
                            var existingHotkey = plugin.pluginHotkeys.Find(h => h.Id == hotkey.Id);
                            if (existingHotkey != null)
                            {
                                // Update existing hotkey
                                existingHotkey.DefaultHotkey = hotkey.DefaultHotkey; // hotkey info provides default values
                                metadata.PluginHotkeys.Add(new PluginHotkey
                                {
                                    Id = hotkey.Id,
                                    DefaultHotkey = hotkey.DefaultHotkey, // hotkey info provides default values
                                    Hotkey = existingHotkey.Hotkey // use settings value
                                });
                            }
                            else
                            {
                                // Add new hotkey if it does not exist
                                plugin.pluginHotkeys.Add(new PluginHotkey
                                {
                                    Id = hotkey.Id,
                                    DefaultHotkey = hotkey.DefaultHotkey, // hotkey info provides default values
                                    Hotkey = hotkey.DefaultHotkey // use default value
                                });
                                metadata.PluginHotkeys.Add(new PluginHotkey
                                {
                                    Id = hotkey.Id,
                                    DefaultHotkey = hotkey.DefaultHotkey, // hotkey info provides default values
                                    Hotkey = hotkey.DefaultHotkey // use default value
                                });
                            }
                        }
                    }
                }
                else
                {
                    // If settings does not exist, create a new one
                    Plugins[metadata.ID] = new Plugin
                    {
                        ID = metadata.ID,
                        Name = metadata.Name,
                        Version = metadata.Version,
                        DefaultActionKeywords = metadata.ActionKeywords, // metadata provides default values
                        ActionKeywords = metadata.ActionKeywords, // use default value
                        Disabled = metadata.Disabled,
                        HomeDisabled = metadata.HomeDisabled,
                        Priority = metadata.Priority,
                        DefaultSearchDelayTime = metadata.SearchDelayTime, // metadata provides default values
                        SearchDelayTime = metadata.SearchDelayTime, // use default value
                    };
                    foreach (var hotkey in hotkeyInfo)
                    {
                        Plugins[metadata.ID].pluginHotkeys.Add(new PluginHotkey
                        {
                            Id = hotkey.Id,
                            DefaultHotkey = hotkey.DefaultHotkey, // hotkey info provides default values
                            Hotkey = hotkey.DefaultHotkey // use default value
                        });
                        metadata.PluginHotkeys.Add(new PluginHotkey
                        {
                            Id = hotkey.Id,
                            DefaultHotkey = hotkey.DefaultHotkey, // hotkey info provides default values
                            Hotkey = hotkey.DefaultHotkey // use default value
                        });
                    }
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

        [JsonIgnore]
        public List<string> DefaultActionKeywords { get; set; }

        // a reference of the action keywords from plugin manager
        public List<string> ActionKeywords { get; set; }

        public int Priority { get; set; }

        [JsonIgnore]
        public int? DefaultSearchDelayTime { get; set; }

        public int? SearchDelayTime { get; set; }

        public List<PluginHotkey> pluginHotkeys { get; set; } = new List<PluginHotkey>();

        /// <summary>
        /// Used only to save the state of the plugin in settings
        /// </summary>
        public bool Disabled { get; set; }
        public bool HomeDisabled { get; set; }
    }
}
