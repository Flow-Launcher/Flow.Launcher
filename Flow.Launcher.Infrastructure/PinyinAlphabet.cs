using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.UserSettings;
using ToolGood.Words.Pinyin;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Infrastructure
{
    public class PinyinAlphabet : IAlphabet
    {
        private readonly ConcurrentDictionary<string, (string translation, TranslationMapping map)> _pinyinCache =
            new();

        private readonly Settings _settings;

        private ReadOnlyDictionary<string, string> currentDoublePinyinTable;

        /// <summary>
        /// Initializes a new instance of the <see cref="PinyinAlphabet"/> class, loading the double Pinyin table based on current settings and subscribing to setting changes to reload the table and clear the cache as needed.
        /// </summary>
        public PinyinAlphabet()
        {
            _settings = Ioc.Default.GetRequiredService<Settings>();
            LoadDoublePinyinTable();

            _settings.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(Settings.UseDoublePinyin) ||
                    e.PropertyName == nameof(Settings.DoublePinyinSchema))
                {
                    Reload();
                }
            };
        }

        /// <summary>
        /// Reloads the double Pinyin mapping table and clears the translation cache.
        /// </summary>
        public void Reload()
        {
            LoadDoublePinyinTable();
            _pinyinCache.Clear();
        }

        /// <summary>
        /// Loads the double Pinyin mapping table for the current schema from a JSON stream.
        /// </summary>
        /// <param name="jsonStream">A stream containing the double Pinyin tables in JSON format.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the current double Pinyin schema is invalid or the table is missing from the JSON.
        /// </exception>
        private void CreateDoublePinyinTableFromStream(Stream jsonStream)
        {
            Dictionary<string, Dictionary<string, string>> table = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(jsonStream);
            if (!table.TryGetValue(_settings.DoublePinyinSchema, out var value))
            {
                throw new InvalidOperationException("DoublePinyinSchema is invalid or double pinyin table is broken.");
            }
            currentDoublePinyinTable = new ReadOnlyDictionary<string, string>(value);
        }

        /// <summary>
        /// Loads the double Pinyin mapping table from a JSON file if enabled in settings; sets an empty table if loading fails or double Pinyin is disabled.
        /// </summary>
        private void LoadDoublePinyinTable()
        {
            if (_settings.UseDoublePinyin)
            {
                var tablePath = Path.Join(AppContext.BaseDirectory, "Resources", "double_pinyin.json");
                try
                {
                    using var fs = File.OpenRead(tablePath);
                    CreateDoublePinyinTableFromStream(fs);
                }
                catch (System.Exception e)
                {
                    Log.Exception(nameof(PinyinAlphabet), "Failed to load double pinyin table from file: " + tablePath, e);
                    currentDoublePinyinTable = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
                }
            }
            else
            {
                currentDoublePinyinTable = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
            }
        }

        /// <summary>
        /// Determines whether the specified string should be translated based on current settings and its content.
        /// </summary>
        /// <param name="stringToTranslate">The string to evaluate for translation eligibility.</param>
        /// <returns>
        /// True if the string contains no Chinese characters and, when double Pinyin is enabled, has an even length; otherwise, false.
        /// </returns>
        public bool ShouldTranslate(string stringToTranslate)
        {
            return _settings.UseDoublePinyin ?
                (!WordsHelper.HasChinese(stringToTranslate) && stringToTranslate.Length % 2 == 0) :
                !WordsHelper.HasChinese(stringToTranslate);
        }

        /// <summary>
        /// Translates the input string to Pinyin, returning the translated string and its mapping.
        /// </summary>
        /// <param name="content">The string to translate.</param>
        /// <returns>
        /// A tuple containing the translated Pinyin string and a <see cref="TranslationMapping"/> object.
        /// If Pinyin translation is disabled or the input contains no Chinese characters, returns the original string and null mapping.
        /// </returns>
        public (string translation, TranslationMapping map) Translate(string content)
        {
            if (!_settings.ShouldUsePinyin || !WordsHelper.HasChinese(content))
                return (content, null);

            return _pinyinCache.TryGetValue(content, out var value)
                ? value
                : BuildCacheFromContent(content);
        }

        /// <summary>
        /// Generates the Pinyin or double Pinyin translation and mapping for the given content.
        /// </summary>
        /// <param name="content">The input string to translate.</param>
        /// <returns>
        /// A tuple containing the translated string and a <see cref="TranslationMapping"/> correlating original and translated indices.
        /// </returns>
        private (string translation, TranslationMapping map) BuildCacheFromContent(string content)
        {
            var resultList = WordsHelper.GetPinyinList(content);

            var resultBuilder = new StringBuilder();
            var map = new TranslationMapping();

            var previousIsChinese = false;

            for (var i = 0; i < resultList.Length; i++)
            {
                if (content[i] >= 0x3400 && content[i] <= 0x9FD5)
                {
                    string translated = _settings.UseDoublePinyin ? ToDoublePin(resultList[i]) : resultList[i];
                    if (previousIsChinese)
                    {
                        map.AddNewIndex(i, resultBuilder.Length, translated.Length + 1);
                        resultBuilder.Append(' ');
                        resultBuilder.Append(translated);
                    }
                    else
                    {
                        map.AddNewIndex(i, resultBuilder.Length, translated.Length);
                        resultBuilder.Append(translated);
                        previousIsChinese = true;
                    }
                }
                else
                {
                    if (previousIsChinese)
                    {
                        previousIsChinese = false;
                        resultBuilder.Append(' ');
                    }
                    resultBuilder.Append(resultList[i]);
                }
            }

            map.endConstruct();

            var key = resultBuilder.ToString();

            return _pinyinCache[content] = (key, map);
        }

        #region Double Pinyin

        /// <summary>
        /// Converts a full Pinyin syllable to its double Pinyin equivalent using the current mapping table.
        /// Returns the original Pinyin if no mapping exists.
        /// </summary>
        /// <param name="fullPinyin">The full Pinyin syllable to convert.</param>
        /// <returns>The double Pinyin equivalent if found; otherwise, the original full Pinyin.</returns>
        private string ToDoublePin(string fullPinyin)
        {
            if (currentDoublePinyinTable.TryGetValue(fullPinyin, out var doublePinyinValue))
            {
                return doublePinyinValue;
            }
            return fullPinyin;
        }

        #endregion
    }
}
