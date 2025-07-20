using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Resource
{
    public class Internationalization
    {
        private static readonly string ClassName = nameof(Internationalization);

        // We should not initialize API in static constructor because it will create another API instance
        private static IPublicAPI api = null;
        private static IPublicAPI API => api ??= Ioc.Default.GetRequiredService<IPublicAPI>();

        private const string Folder = "Languages";
        private const string DefaultLanguageCode = "en";
        private const string DefaultFile = "en.xaml";
        private const string Extension = ".xaml";
        private readonly Settings _settings;
        private readonly List<string> _languageDirectories = new();
        private readonly List<ResourceDictionary> _oldResources = new();
        private static string SystemLanguageCode;

        public Internationalization(Settings settings)
        {
            _settings = settings;
        }

        public static void InitSystemLanguageCode()
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
                    SystemLanguageCode = languageCode;
                }
            }

            SystemLanguageCode = DefaultLanguageCode;
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

            // Add Flow Launcher language directory
            AddFlowLauncherLanguageDirectory();

            // Add plugin language directories first so that we can load language files from plugins
            AddPluginLanguageDirectories();

            // Load default language resources
            LoadDefaultLanguage();

            // Change language
            await ChangeLanguageAsync(language, false);
        }

        private void AddFlowLauncherLanguageDirectory()
        {
            // Check if Flow Launcher language directory exists
            var directory = Path.Combine(Constant.ProgramDirectory, Folder);
            if (!Directory.Exists(directory))
            {
                API.LogError(ClassName, $"Flow Launcher language directory can't be found <{directory}>");
                return;
            }

            // Check if the language directory contains default language file
            if (!File.Exists(Path.Combine(directory, DefaultFile)))
            {
                API.LogError(ClassName, $"Default language file can't be found in path <{directory}>");
                return;
            }

            _languageDirectories.Add(directory);
        }

        private void AddPluginLanguageDirectories()
        {
            foreach (var pluginsDir in PluginManager.Directories)
            {
                if (!Directory.Exists(pluginsDir)) continue;

                // Enumerate all top directories in the plugin directory
                foreach (var dir in Directory.GetDirectories(pluginsDir))
                {
                    // Check if the directory contains a language folder
                    var pluginLanguageDir = Path.Combine(dir, Folder);
                    if (!Directory.Exists(pluginLanguageDir)) continue;

                    // Check if the language directory contains default language file
                    if (File.Exists(Path.Combine(pluginLanguageDir, DefaultFile)))
                    {
                        // Add the plugin language directory to the list
                        _languageDirectories.Add(pluginLanguageDir);
                    }
                    else
                    {
                        API.LogError(ClassName, $"Can't find default language file in path <{pluginLanguageDir}>");
                    }
                }
            }
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

        private static Language GetLanguageByLanguageCode(string languageCode)
        {
            var lowercase = languageCode.ToLower();
            var language = AvailableLanguages.GetAvailableLanguages().FirstOrDefault(o => o.LanguageCode.Equals(lowercase, StringComparison.CurrentCultureIgnoreCase));
            if (language == null)
            {
                API.LogError(ClassName, $"Language code can't be found <{languageCode}>");
                return AvailableLanguages.English;
            }
            else
            {
                return language;
            }
        }

        private async Task ChangeLanguageAsync(Language language, bool updateMetadata = true)
        {
            // Remove old language files and load language
            RemoveOldLanguageFiles();
            if (language != AvailableLanguages.English)
            {
                LoadLanguage(language);
            }

            // Change culture info
            ChangeCultureInfo(language.LanguageCode);

            if (updateMetadata)
            {
                // Raise event for plugins after culture is set
                await Task.Run(UpdatePluginMetadataTranslations);
            }
        }

        public static void ChangeCultureInfo(string languageCode)
        {
            // Culture of main thread
            // Use CreateSpecificCulture to preserve possible user-override settings in Windows, if Flow's language culture is the same as Windows's
            CultureInfo currentCulture;
            try
            {
                currentCulture = CultureInfo.CreateSpecificCulture(languageCode);
            }
            catch (CultureNotFoundException)
            {
                currentCulture = CultureInfo.CreateSpecificCulture(SystemLanguageCode);
            }
            CultureInfo.CurrentCulture = currentCulture;
            CultureInfo.CurrentUICulture = currentCulture;
            var thread = Thread.CurrentThread;
            thread.CurrentCulture = currentCulture;
            thread.CurrentUICulture = currentCulture;
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
            string text = languageToSet == AvailableLanguages.Chinese ? "是否启用拼音搜索？" : "是否啓用拼音搜索？";

            if (API.ShowMsgBox(text, string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.No)
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

        public static string GetTranslation(string key)
        {
            var translation = Application.Current.TryFindResource(key);
            if (translation is string)
            {
                return translation.ToString();
            }
            else
            {
                API.LogError(ClassName, $"No Translation for key {key}");
                return $"No Translation for key {key}";
            }
        }

        public static void UpdatePluginMetadataTranslations()
        {
            // Update plugin metadata name & description
            foreach (var p in PluginManager.GetTranslationPlugins())
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
                    API.LogException(ClassName, $"Failed for <{p.Metadata.Name}>", e);
                }
            }
        }

        private static string LanguageFile(string folder, string language)
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
                    API.LogError(ClassName, $"Language path can't be found <{path}>");
                    var english = Path.Combine(folder, DefaultFile);
                    if (File.Exists(english))
                    {
                        return english;
                    }
                    else
                    {
                        API.LogError(ClassName, $"Default English Language path can't be found <{path}>");
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
