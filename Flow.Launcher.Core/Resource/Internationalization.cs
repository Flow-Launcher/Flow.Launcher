using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Resource
{
    public partial class Internationalization : IDisposable
    {
        private static readonly string ClassName = nameof(Internationalization);

        private const string Folder = "Languages";
        private const string DefaultLanguageCode = "en";
        private const string DefaultFile = "en.xaml";
        private const string Extension = ".xaml";
        private readonly Settings _settings;
        private readonly List<string> _languageDirectories = [];
        private static string SystemLanguageCode;
        private readonly SemaphoreSlim _langChangeLock = new(1, 1);

        public Internationalization(Settings settings)
        {
            _settings = settings;
        }

        #region Initialization

        /// <summary>
        /// Initialize the system language code based on the current culture.
        /// </summary>
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
                    return;
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
                PublicApi.Instance.LogError(ClassName, $"Flow Launcher language directory can't be found <{directory}>");
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

                    // Check if the language directory contains default language file since it will be checked later
                    _languageDirectories.Add(pluginLanguageDir);
                }
            }
        }

        // Language xaml files are merged into WPF Application resources (Windows only); the Avalonia app loads translations itself.
        partial void LoadDefaultLanguage();

        #endregion

        #region Change Language

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
            var language = AvailableLanguages.GetAvailableLanguages().
                FirstOrDefault(o => o.LanguageCode.Equals(languageCode, StringComparison.OrdinalIgnoreCase));
            if (language == null)
            {
                PublicApi.Instance.LogError(ClassName, $"Language code can't be found <{languageCode}>");
                return AvailableLanguages.English;
            }
            else
            {
                return language;
            }
        }

        private async Task ChangeLanguageAsync(Language language, bool updateMetadata = true)
        {
            await _langChangeLock.WaitAsync();

            try
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
            catch (Exception e)
            {
                PublicApi.Instance.LogException(ClassName, $"Failed to change language to <{language.LanguageCode}>", e);
            }
            finally
            {
                _langChangeLock.Release();
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

        #endregion

        #region Prompt Language Options

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

            if (PublicApi.Instance.ShowMsgBox(text, string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.No)
                return false;

            return true;
        }

        public bool PromptShouldIgnoreAccents(string languageCodeToSet)
        {
            var languageToSet = GetLanguageByLanguageCode(languageCodeToSet);

            if (_settings.IgnoreAccents)
                return false;

            // Languages that commonly use diacritics / accents
            var languagesWithDiacritics = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                AvailableLanguages.French.LanguageCode,
                AvailableLanguages.Polish.LanguageCode,
                AvailableLanguages.Slovak.LanguageCode,
                AvailableLanguages.Czech.LanguageCode,
                AvailableLanguages.Portuguese_Portugal.LanguageCode,
                AvailableLanguages.Portuguese_Brazil.LanguageCode,
                AvailableLanguages.Spanish.LanguageCode,
                AvailableLanguages.Spanish_LatinAmerica.LanguageCode,
                AvailableLanguages.Turkish.LanguageCode,
                AvailableLanguages.Dutch.LanguageCode,
                AvailableLanguages.German.LanguageCode,
                AvailableLanguages.Serbian.LanguageCode,
                AvailableLanguages.Italian.LanguageCode,
                AvailableLanguages.Danish.LanguageCode,
                AvailableLanguages.Norwegian_Bokmal.LanguageCode
            };

            if (!languagesWithDiacritics.Contains(languageToSet.LanguageCode))
                return false;

            if (PublicApi.Instance.ShowMsgBox(Localize.promptIgnoreAccents(), string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.No)
                return false;

            return true;
        }

        #endregion

        #region Language Resources Management

        partial void RemoveOldLanguageFiles();

        partial void LoadLanguage(Language language);

        #endregion

        #region Available Languages

        public List<Language> LoadAvailableLanguages()
        {
            var list = AvailableLanguages.GetAvailableLanguages();
            list.Insert(0, new Language(Constant.SystemLanguageCode, AvailableLanguages.GetSystemTranslation(SystemLanguageCode)));
            return list;
        }

        #endregion

        #region Update Metadata

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
                    PublicApi.Instance.LogException(ClassName, $"Failed for <{p.Metadata.Name}>", e);
                }
            }
        }

        public static void UpdatePluginMetadataTranslation(PluginPair p)
        {
            // Update plugin metadata name & description
            if (p.Plugin is not IPluginI18n pluginI18N) return;
            try
            {
                p.Metadata.Name = pluginI18N.GetTranslatedPluginTitle();
                p.Metadata.Description = pluginI18N.GetTranslatedPluginDescription();
                pluginI18N.OnCultureInfoChanged(CultureInfo.CurrentCulture);
            }
            catch (Exception e)
            {
                PublicApi.Instance.LogException(ClassName, $"Failed for <{p.Metadata.Name}>", e);
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            RemoveOldLanguageFiles();
            _langChangeLock.Dispose();
        }

        #endregion
    }
}
