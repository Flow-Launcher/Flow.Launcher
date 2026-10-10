using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Flow.Launcher.Infrastructure;

namespace Flow.Launcher.Core.Resource
{
    public partial class Internationalization
    {
        private readonly List<ResourceDictionary> _oldResources = [];

        partial void LoadDefaultLanguage()
        {
            // Removes language files loaded before any plugins were loaded.
            // Prevents the language Flow started in from overwriting English if the user switches back to English
            RemoveOldLanguageFiles();
            LoadLanguage(AvailableLanguages.English);
            _oldResources.Clear();
        }

        #region Language Resources Management

        partial void RemoveOldLanguageFiles()
        {
            var dicts = Application.Current.Resources.MergedDictionaries;
            foreach (var r in _oldResources)
            {
                dicts.Remove(r);
            }
            _oldResources.Clear();
        }

        partial void LoadLanguage(Language language)
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
                    PublicApi.Instance.LogError(ClassName, $"Language path can't be found <{path}>");
                    var english = Path.Combine(folder, DefaultFile);
                    if (File.Exists(english))
                    {
                        return english;
                    }
                    else
                    {
                        PublicApi.Instance.LogError(ClassName, $"Default English Language path can't be found <{path}>");
                        return string.Empty;
                    }
                }
            }
            else
            {
                return string.Empty;
            }
        }

        #endregion

        #region Get Translations

        public static string GetTranslation(string key)
        {
            var translation = Application.Current.TryFindResource(key);
            if (translation is string)
            {
                return translation.ToString();
            }
            else
            {
                PublicApi.Instance.LogError(ClassName, $"No Translation for key {key}");
                return $"No Translation for key {key}";
            }
        }

        #endregion
    }
}
