using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin;

#nullable enable

namespace Flow.Launcher.Core.Plugin
{
    public partial class JsonRPCPluginSettings : ISavable
    {
        public required JsonRpcConfigurationModel? Configuration { get; init; }

        public required string SettingPath { get; init; }

        public IReadOnlyDictionary<string, object?> Inner => Settings;
        protected ConcurrentDictionary<string, object?> Settings { get; set; } = null!;
        public required IPublicAPI API { get; init; }

        private static readonly string ClassName = nameof(JsonRPCPluginSettings);

        private JsonStorage<ConcurrentDictionary<string, object?>> _storage = null!;

        public async Task InitializeAsync()
        {
            if (Settings == null)
            {
                _storage = new JsonStorage<ConcurrentDictionary<string, object?>>(SettingPath);
                Settings = await _storage.LoadAsync();

                // Because value type of settings dictionary is object which causes them to be JsonElement when loading from json files,
                // we need to convert it to the correct type
                foreach (var (key, value) in Settings)
                {
                    if (value is not JsonElement jsonElement) continue;

                    Settings[key] = jsonElement.ValueKind switch
                    {
                        JsonValueKind.String => jsonElement.GetString() ?? value,
                        JsonValueKind.True => jsonElement.GetBoolean(),
                        JsonValueKind.False => jsonElement.GetBoolean(),
                        JsonValueKind.Null => null,
                        _ => value
                    };
                }
            }

            if (Configuration == null) return;

            foreach (var (type, attributes) in Configuration.Body)
            {
                // Skip if the setting does not have attributes or name
                if (attributes?.Name == null) continue;

                // Skip if the setting does not have attributes or name
                if (!NeedSaveInSettings(type)) continue;

                // If need save in settings, we need to make sure the setting exists in the settings file
                if (Settings.ContainsKey(attributes.Name)) continue;

                if (type == "checkbox")
                {
                    // If can parse the default value to bool, use it, otherwise use false
                    Settings[attributes.Name] = bool.TryParse(attributes.DefaultValue, out var value) && value;
                }
                else
                {
                    Settings[attributes.Name] = attributes.DefaultValue;
                }
            }
        }

        public void UpdateSettings(IReadOnlyDictionary<string, object> settings)
        {
            if (settings == null || settings.Count == 0) return;

            foreach (var (key, value) in settings)
            {
                Settings[key] = value;

                UpdateSettingControl(key, value);
            }

            Save();
        }

        public async Task SaveAsync()
        {
            try
            {
                await _storage.SaveAsync();
            }
            catch (System.Exception e)
            {
                API.LogException(ClassName, $"Failed to save plugin settings to path: {SettingPath}", e);
            }
        }

        public void Save()
        {
            try
            {
                _storage.Save();
            }
            catch (System.Exception e)
            {
                API.LogException(ClassName, $"Failed to save plugin settings to path: {SettingPath}", e);
            }
        }
        
        public bool NeedCreateSettingPanel()
        {
            // If there are no settings or the settings configuration is empty, return null
            return Settings != null && Configuration != null && Configuration.Body.Count != 0;
        }

        // Updates the WPF setting panel control bound to the key; no WPF panel exists on non-Windows.
        partial void UpdateSettingControl(string key, object? value);

        private static bool NeedSaveInSettings(string type)
        {
            return type != "textBlock" && type != "separator" && type != "hyperlink";
        }
    }
}
