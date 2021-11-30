// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.WindowsSettings.Classes;
using Flow.Launcher.Plugin.WindowsSettings.Helper;
using Flow.Launcher.Plugin.WindowsSettings.Properties;

namespace Flow.Launcher.Plugin.WindowsSettings
{
    /// <summary>
    /// Main class of this plugin that implement all used interfaces.
    /// </summary>
    public sealed class Main : IPlugin, IContextMenu, IPluginI18n, IDisposable
    {
        /// <summary>
        /// The path to the symbol for a light theme.
        /// </summary>
        private const string _lightSymbol = "Images/WindowsSettings.light.png";

        /// <summary>
        /// The path to the symbol for a dark theme.
        /// </summary>
        private const string _darkSymbol = "Images/WindowsSettings.dark.png";

        /// <summary>
        /// The name of this assembly.
        /// </summary>
        private readonly string _assemblyName;

        /// <summary>
        /// The initial context for this plugin (contains API and meta-data).
        /// </summary>
        private PluginInitContext? _context;

        /// <summary>
        /// The path to the icon for each result.
        /// </summary>
        private string _defaultIconPath;

        /// <summary>
        /// Indicate that the plugin is disposed.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// List that contain all settings.
        /// </summary>
        private IEnumerable<WindowsSetting>? _settingsList;

        /// <summary>
        /// List that contains translated string
        /// </summary>
        private IEnumerable<WindowsSetting> _translatedSettingList = new List<WindowsSetting>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Main"/> class.
        /// </summary>
        public Main()
        {
            _assemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? Name;
            _defaultIconPath = _lightSymbol;
        }

        /// <summary>
        /// Gets the localized name.
        /// </summary>
        public string Name => Resources.PluginTitle;

        /// <summary>
        /// Gets the localized description.
        /// </summary>
        public string Description => Resources.PluginDescription;

        /// <summary>
        /// Initialize the plugin with the given <see cref="PluginInitContext"/>.
        /// </summary>
        /// <param name="context">The <see cref="PluginInitContext"/> for this plugin.</param>
        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            _settingsList = JsonSettingsListHelper.ReadAllPossibleSettings();
            _settingsList = UnsupportedSettingsHelper.FilterByBuild(_settingsList);

            Log.Init(_context.API);
            ResultHelper.Init(_context.API);

            _translatedSettingList = TranslationHelper.TranslateAllSettings(_settingsList);
        }

        /// <summary>
        /// Return a filtered list, based on the given query.
        /// </summary>
        /// <param name="query">The query to filter the list.</param>
        /// <returns>A filtered list, can be empty when nothing was found.</returns>
        public List<Result> Query(Query query)
         {
            var newList = ResultHelper.GetResultList(_translatedSettingList, query, _defaultIconPath);
            return newList;

            
        }

        public void OnCultureInfoChanged(CultureInfo newCulture)
        {
            _translatedSettingList = TranslationHelper.TranslateAllSettings(_settingsList);
        }

        /// <summary>
        /// Return a list context menu entries for a given <see cref="Result"/> (shown at the right side of the result).
        /// </summary>
        /// <param name="selectedResult">The <see cref="Result"/> for the list with context menu entries.</param>
        /// <returns>A list context menu entries.</returns>
        public List<Result> LoadContextMenus(Result selectedResult)
        {
            return ContextMenuHelper.GetContextMenu(selectedResult, _assemblyName);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Wrapper method for <see cref="Dispose"/> that dispose additional objects and events form the plugin itself.
        /// </summary>
        /// <param name="disposing">Indicate that the plugin is disposed.</param>
        private void Dispose(bool disposing)
        {
            if (_disposed || !disposing)
            {
                return;
            }

            _disposed = true;
        }

        /// <summary>
        /// Gets the localized name.
        /// </summary>
        public string GetTranslatedPluginTitle()
        {
            return Name;
        }

        /// <summary>
        /// Gets the localized description.
        /// </summary>
        public string GetTranslatedPluginDescription()
        {
            return Description;
        }
    }
}
