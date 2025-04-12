﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Launcher.Plugin.WindowsSettings.Classes;

namespace Flow.Launcher.Plugin.WindowsSettings.Helper
{
    /// <summary>
    /// Helper class to easier work with the JSON file that contains all Windows settings
    /// </summary>
    internal static class JsonSettingsListHelper
    {
        /// <summary>
        /// The name of the file that contains all settings for the query
        /// </summary>
        private const string _settingsFile = "WindowsSettings.json";

        /// <summary>
        /// Read all possible Windows settings.
        /// </summary>
        /// <returns>A list with all possible windows settings.</returns>
        internal static IEnumerable<WindowsSetting> ReadAllPossibleSettings()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var type = assembly.GetTypes().FirstOrDefault(x => x.Name == nameof(Main));

            IEnumerable<WindowsSetting>? settingsList = null;

            try
            {
                var resourceName = $"{type?.Namespace}.{_settingsFile}";
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream is null)
                {
                    throw new Exception("stream is null");
                }

                var options = new JsonSerializerOptions();
                options.Converters.Add(new JsonStringEnumConverter());

                using var reader = new StreamReader(stream);
                var text = reader.ReadToEnd();

                settingsList = JsonSerializer.Deserialize<IEnumerable<WindowsSetting>>(text, options);
            }
            catch (Exception exception)
            {
                Log.Exception("Error loading settings JSON file", exception, typeof(Main));
            }

            return settingsList ?? Enumerable.Empty<WindowsSetting>();
        }
    }
}
