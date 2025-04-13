﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Flow.Launcher.Core.Resource
{
    public class Internationalization
    {
        private static readonly string ClassName = nameof(Internationalization);

        private const string Folder = "Languages";
        private const string DefaultLanguageCode = "en";
        private const string DefaultFile = "en.xaml";
        private const string Extension = ".xaml";
        private readonly Settings _settings;
        private readonly IPublicAPI _api;
        private readonly List<string> _languageDirectories = new();
        private readonly List<ResourceDictionary> _oldResources = new();
        private readonly string SystemLanguageCode;

        public Internationalization(Settings settings)
        {
            _settings = settings;
            _api = Ioc.Default.GetRequiredService<IPublicAPI>();
            AddFlowLauncherLanguageDirectory();
            SystemLanguageCode = GetSystemLanguageCodeAtStartup();
        }

        private void AddFlowLauncherLanguageDirectory()
        {
            var directory = Path.Combine(Constant.ProgramDirectory, Folder);
            _languageDirectories.Add(directory);
        }

        private static string GetSystemLanguageCodeAtStartup()
        {
            var availableLanguages = AvailableLanguages.GetAvailableLanguages();

            // Retrieve the language identifiers for the current culture.
            // ChangeLanguage method overrides the CultureInfo.CurrentCulture, so this needs to
            // be called at startup in order to get the correct lang code of system. 
            var currentCulture = CultureInfo.CurrentCulture;
            var twoLetterCode = currentCulture.TwoLetterISOLanguageName;
            var threeLetterCode = currentCulture.ThreeLetterISOLanguageName;
            var fullName = currentCulture.Name;

            // Try to find a match in the available languages list
            foreach (var language in availableLanguages)
            {
                var languageCode = language.LanguageCode;

                if (string.Equals(languageCode, twoLetterCode, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(languageCode, threeLetterCode, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(languageCode, fullName, StringComparison.OrdinalIgnoreCase))
                {
                    return languageCode;
                }
            }

            return DefaultLanguageCode;
        }

        private void AddPluginLanguageDirectories()
        {
            foreach (var plugin in PluginManager.GetPluginsForInterface<IPluginI18n>())
            {
                var location = Assembly.GetAssembly(plugin.Plugin.GetType()).Location;
                var dir = Path.GetDirectoryName(location);
                if (dir != null)
                {
                    var pluginThemeDirectory = Path.Combine(dir, Folder);
                    _languageDirectories.Add(pluginThemeDirectory);
                }
                else
                {
                    _api.LogError(ClassName, $"Can't find plugin path <{location}> for <{plugin.Metadata.Name}>");
                }
            }

            LoadDefaultLanguage();
        }

        private void LoadDefaultLanguage()
        {
            // Removes language files loaded before any plugins were loaded.
            // Prevents the language Flow started in from overwriting English if the user switches back to English
            RemoveOldLanguageFiles();
            LoadLanguage(AvailableLanguages.English);
            _oldResources.Clear();
        }

        /// <summary>
        /// Initialize language. Will change app language and plugin language based on settings.
        /// </summary>
        public async Task InitializeLanguageAsync()
        {
            // Get actual language
            var languageCode = _settings.Language;
            if (languageCode == Constant.SystemLanguageCode)
            {
                languageCode = SystemLanguageCode;
            }

            // Get language by language code and change language
            var language = GetLanguageByLanguageCode(languageCode);

            // Add plugin language directories first so that we can load language files from plugins
            AddPluginLanguageDirectories();

            // Change language
            await ChangeLanguageAsync(language);
        }

        /// <summary>
        /// Change language during runtime. Will change app language and plugin language & save settings.
        /// </summary>
        /// <param name="languageCode"></param>
        public void ChangeLanguage(string languageCode)
        {
            languageCode = languageCode.NonNull();

            // Get actual language if language code is system
            var isSystem = false;
            if (languageCode == Constant.SystemLanguageCode)
            {
                languageCode = SystemLanguageCode;
                isSystem = true;
            }

            // Get language by language code and change language
            var language = GetLanguageByLanguageCode(languageCode);

            // Change language
            _ = ChangeLanguageAsync(language);

            // Save settings
            _settings.Language = isSystem ? Constant.SystemLanguageCode : language.LanguageCode;
        }

        private Language GetLanguageByLanguageCode(string languageCode)
        {
            var lowercase = languageCode.ToLower();
            var language = AvailableLanguages.GetAvailableLanguages().FirstOrDefault(o => o.LanguageCode.ToLower() == lowercase);
            if (language == null)
            {
                _api.LogError(ClassName, $"Language code can't be found <{languageCode}>");
                return AvailableLanguages.English;
            }
            else
            {
                return language;
            }
        }

        private async Task ChangeLanguageAsync(Language language)
        {
            // Remove old language files and load language
            RemoveOldLanguageFiles();
            if (language != AvailableLanguages.English)
            {
                LoadLanguage(language);
            }

            // Culture of main thread
            // Use CreateSpecificCulture to preserve possible user-override settings in Windows, if Flow's language culture is the same as Windows's
            CultureInfo.CurrentCulture = CultureInfo.CreateSpecificCulture(language.LanguageCode);
            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;

            // Raise event for plugins after culture is set
            await Task.Run(UpdatePluginMetadataTranslations);
        }

        public bool PromptShouldUsePinyin(string languageCodeToSet)
        {
            var languageToSet = GetLanguageByLanguageCode(languageCodeToSet);

            if (_settings.ShouldUsePinyin)
                return false;

            if (languageToSet != AvailableLanguages.Chinese && languageToSet != AvailableLanguages.Chinese_TW)
                return false;

            // No other languages should show the following text so just make it hard-coded
            // "Do you want to search with pinyin?"
            string text = languageToSet == AvailableLanguages.Chinese ? "是否启用拼音搜索？" : "是否啓用拼音搜索？" ;

            if (Ioc.Default.GetRequiredService<IPublicAPI>().ShowMsgBox(text, string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.No)
                return false;

            return true;
        }

        private void RemoveOldLanguageFiles()
        {
            var dicts = Application.Current.Resources.MergedDictionaries;
            foreach (var r in _oldResources)
            {
                dicts.Remove(r);
            }
        }

        private void LoadLanguage(Language language)
        {
            var flowEnglishFile = Path.Combine(Constant.ProgramDirectory, Folder, DefaultFile);
            var dicts = Application.Current.Resources.MergedDictionaries;
            var filename = $"{language.LanguageCode}{Extension}";
            var files = _languageDirectories
                .Select(d => LanguageFile(d, filename))
                // Exclude Flow's English language file since it's built into the binary, and there's no need to load
                // it again from the file system.
                .Where(f => !string.IsNullOrEmpty(f) && f != flowEnglishFile)
                .ToArray();

            if (files.Length > 0)
            {
                foreach (var f in files)
                {
                    var r = new ResourceDictionary
                    {
                        Source = new Uri(f, UriKind.Absolute)
                    };
                    dicts.Add(r);
                    _oldResources.Add(r);
                }
            }
        }

        public List<Language> LoadAvailableLanguages()
        {
            var list = AvailableLanguages.GetAvailableLanguages();
            list.Insert(0, new Language(Constant.SystemLanguageCode, AvailableLanguages.GetSystemTranslation(SystemLanguageCode)));
            return list;
        }

        public string GetTranslation(string key)
        {
            var translation = Application.Current.TryFindResource(key);
            if (translation is string)
            {
                return translation.ToString();
            }
            else
            {
                _api.LogError(ClassName, $"No Translation for key {key}");
                return $"No Translation for key {key}";
            }
        }

        private void UpdatePluginMetadataTranslations()
        {
            foreach (var p in PluginManager.GetPluginsForInterface<IPluginI18n>())
            {
                if (p.Plugin is not IPluginI18n pluginI18N) return;
                try
                {
                    p.Metadata.Name = pluginI18N.GetTranslatedPluginTitle();
                    p.Metadata.Description = pluginI18N.GetTranslatedPluginDescription();
                    pluginI18N.OnCultureInfoChanged(CultureInfo.CurrentCulture);
                }
                catch (Exception e)
                {
                    _api.LogException(ClassName, $"Failed for <{p.Metadata.Name}>", e);
                }
            }
        }

        private string LanguageFile(string folder, string language)
        {
            if (Directory.Exists(folder))
            {
                var path = Path.Combine(folder, language);
                if (File.Exists(path))
                {
                    return path;
                }
                else
                {
                    _api.LogError(ClassName, $"Language path can't be found <{path}>");
                    var english = Path.Combine(folder, DefaultFile);
                    if (File.Exists(english))
                    {
                        return english;
                    }
                    else
                    {
                        _api.LogError(ClassName, $"Default English Language path can't be found <{path}>");
                        return string.Empty;
                    }
                }
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
