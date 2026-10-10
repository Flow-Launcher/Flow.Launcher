using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.UserSettings;
using ToolGood.Words.Pinyin;
using Flow.Launcher.Infrastructure.Logger;
using System.Threading;

namespace Flow.Launcher.Infrastructure
{
    public class PinyinAlphabet : IAlphabet
    {
        private sealed record PinyinConfiguration(
            long Revision,
            bool ShouldUsePinyin,
            bool UseDoublePinyin,
            ReadOnlyDictionary<string, string> DoublePinyinTable,
            ReadOnlyDictionary<string, string[]> PolyphonicPhraseOverrides,
            int MaxPolyphonicPhraseLength);

        private readonly record struct CachedTranslation(
            long Revision,
            string Translation,
            TranslationMapping Map);

        private readonly Lock _configurationLock = new();
        private readonly Settings _settings;
        private PinyinConfiguration _configuration;
        private readonly Dictionary<string, CachedTranslation> _pinyinCache = new();
        private readonly Func<string, string[]> _getPinyinList;
        
        public PinyinAlphabet()
            : this(Ioc.Default.GetRequiredService<Settings>())
        {
        }

        public PinyinAlphabet(Settings settings)
            : this(settings, content => WordsHelper.GetPinyinList(content))
        {
        }

        internal PinyinAlphabet(Settings settings, Func<string, string[]> getPinyinList)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(getPinyinList);

            _settings = settings;
            _getPinyinList = getPinyinList;
            _configuration = CreateConfiguration(0);

            _settings.PropertyChanged += (sender, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(Settings.ShouldUsePinyin):
                    case nameof(Settings.UseDoublePinyin):
                    case nameof(Settings.UsePolyphonicPhraseOverrides):
                    case nameof(Settings.DoublePinyinSchema):
                        Reload();
                        break;
                }
            };
        }

        public void Reload()
        {
            lock (_configurationLock)
            {
                _configuration = CreateConfiguration(_configuration.Revision + 1);
                _pinyinCache.Clear();
            }
        }

        private PinyinConfiguration CreateConfiguration(long revision)
        {
            var shouldUsePinyin = _settings.ShouldUsePinyin;
            var useDoublePinyin = _settings.UseDoublePinyin;
            var doublePinyinSchema = _settings.DoublePinyinSchema;
            var usePolyphonicPhraseOverrides = _settings.UsePolyphonicPhraseOverrides;
            var doublePinyinTable = LoadDoublePinyinTable(useDoublePinyin, doublePinyinSchema);
            var (polyphonicPhraseOverrides, maxPolyphonicPhraseLength) =
                LoadPolyphonicPhraseOverrides(shouldUsePinyin, usePolyphonicPhraseOverrides);

            return new PinyinConfiguration(
                revision,
                shouldUsePinyin,
                useDoublePinyin,
                doublePinyinTable,
                polyphonicPhraseOverrides,
                maxPolyphonicPhraseLength);
        }

        private static JsonSerializerOptions GetOptions()
        {
            return new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip
            };
        }

        private static (ReadOnlyDictionary<string, string[]> overrides, int maxPhraseLength)
            CreatePolyphonicPhraseOverridesFromStream(Stream jsonStream, JsonSerializerOptions options)
        {
            var overrides = JsonSerializer.Deserialize<Dictionary<string, string[]>>(jsonStream, options) ??
                throw new InvalidOperationException("Failed to deserialize polyphonic pinyin phrase overrides: result is null");

            var maxPhraseLength = 0;
            foreach (var phrase in overrides.Keys)
            {
                maxPhraseLength = Math.Max(maxPhraseLength, phrase.Length);
            }

            return (new ReadOnlyDictionary<string, string[]>(overrides), maxPhraseLength);
        }

        private static (ReadOnlyDictionary<string, string[]> overrides, int maxPhraseLength)
            LoadPolyphonicPhraseOverrides(bool shouldUsePinyin, bool usePolyphonicPhraseOverrides)
        {
            if (!shouldUsePinyin || !usePolyphonicPhraseOverrides)
            {
                return EmptyPolyphonicPhraseOverrides();
            }

            var overridesPath = Path.Combine(AppContext.BaseDirectory, "Resources", "polyphonic_pinyin.json");
            try
            {
                using var fs = File.OpenRead(overridesPath);
                return CreatePolyphonicPhraseOverridesFromStream(fs, GetOptions());
            }
            catch (System.Exception e)
            {
                Log.Exception(nameof(PinyinAlphabet), $"Failed to load polyphonic pinyin phrase overrides from file: {overridesPath}", e);
                return EmptyPolyphonicPhraseOverrides();
            }
        }

        private static (ReadOnlyDictionary<string, string[]> overrides, int maxPhraseLength) EmptyPolyphonicPhraseOverrides() =>
            (new ReadOnlyDictionary<string, string[]>(new Dictionary<string, string[]>()), 0);

        private static ReadOnlyDictionary<string, string> CreateDoublePinyinTableFromStream(
            Stream jsonStream,
            DoublePinyinSchemas doublePinyinSchema)
        {
            var table = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(jsonStream, GetOptions()) ??
                throw new InvalidOperationException("Failed to deserialize double pinyin table: result is null");

            var schemaKey = doublePinyinSchema.ToString();
            if (!table.TryGetValue(schemaKey, out var schemaDict))
            {
                throw new ArgumentException($"DoublePinyinSchema '{schemaKey}' is invalid or double pinyin table is broken.");
            }

            return new ReadOnlyDictionary<string, string>(schemaDict);
        }

        private static ReadOnlyDictionary<string, string> LoadDoublePinyinTable(
            bool useDoublePinyin,
            DoublePinyinSchemas doublePinyinSchema)
        {
            if (!useDoublePinyin)
            {
                return EmptyDoublePinyinTable();
            }

            var tablePath = Path.Combine(AppContext.BaseDirectory, "Resources", "double_pinyin.json");
            try
            {
                using var fs = File.OpenRead(tablePath);
                return CreateDoublePinyinTableFromStream(fs, doublePinyinSchema);
            }
            catch (FileNotFoundException e)
            {
                Log.Exception(nameof(PinyinAlphabet), $"Double pinyin table file not found: {tablePath}", e);
                return EmptyDoublePinyinTable();
            }
            catch (DirectoryNotFoundException e)
            {
                Log.Exception(nameof(PinyinAlphabet), $"Directory not found for double pinyin table: {tablePath}", e);
                return EmptyDoublePinyinTable();
            }
            catch (UnauthorizedAccessException e)
            {
                Log.Exception(nameof(PinyinAlphabet), $"Access denied to double pinyin table: {tablePath}", e);
                return EmptyDoublePinyinTable();
            }
            catch (System.Exception e)
            {
                Log.Exception(nameof(PinyinAlphabet), $"Failed to load double pinyin table from file: {tablePath}", e);
                return EmptyDoublePinyinTable();
            }
        }

        private static ReadOnlyDictionary<string, string> EmptyDoublePinyinTable() =>
            new(new Dictionary<string, string>());

        public bool ShouldTranslate(string stringToTranslate)
        {
            // If the query (stringToTranslate) does NOT contain Chinese characters, 
            // we should translate the target string to pinyin for matching
            lock (_configurationLock)
            {
                return _configuration.ShouldUsePinyin && !ContainsChinese(stringToTranslate);
            }
        }

        public (string translation, TranslationMapping map) Translate(string content)
        {
            if (!ContainsChinese(content))
                return (content, null);

            while (true)
            {
                PinyinConfiguration configuration;
                lock (_configurationLock)
                {
                    configuration = _configuration;
                    if (!configuration.ShouldUsePinyin)
                    {
                        return (content, null);
                    }

                    if (_pinyinCache.TryGetValue(content, out var cached) && cached.Revision == configuration.Revision)
                    {
                        return (cached.Translation, cached.Map);
                    }
                }

                var result = BuildCacheFromContent(content, configuration);

                lock (_configurationLock)
                {
                    // A settings reload may have completed while this translation was being built.
                    // In that case, discard the stale result and rebuild from the latest snapshot.
                    if (_configuration.Revision != configuration.Revision)
                    {
                        continue;
                    }

                    _pinyinCache[content] = new CachedTranslation(configuration.Revision, result.translation, result.map);
                    return result;
                }
            }
        }

        private (string translation, TranslationMapping map) BuildCacheFromContent(
            string content,
            PinyinConfiguration configuration)
        {
            var resultList = _getPinyinList(content);
            ApplyPolyphonicPhraseOverrides(content, resultList, configuration);

            var resultBuilder = new StringBuilder(configuration.UseDoublePinyin ? 3 : 4); // Pre-allocate with estimated capacity
            var map = new TranslationMapping();

            var previousIsChinese = false;

            for (var i = 0; i < resultList.Length; i++)
            {
                if (IsChineseCharacter(content[i]))
                {
                    var translated = configuration.UseDoublePinyin
                        ? ToDoublePinyin(resultList[i], configuration.DoublePinyinTable)
                        : resultList[i];

                    if (i > 0 && content[i - 1] != ' ')
                    {
                        resultBuilder.Append(' ');
                    }

                    map.AddNewIndex(resultBuilder.Length, translated.Length);
                    resultBuilder.Append(translated);
                    previousIsChinese = true;
                }
                else
                {
                    // Add space after Chinese characters before non-Chinese characters
                    if (previousIsChinese)
                    {
                        previousIsChinese = false;
                        if (content[i] != ' ')
                        {
                            resultBuilder.Append(' ');
                        }
                    }

                    map.AddNewIndex(resultBuilder.Length, 1);
                    resultBuilder.Append(content[i]);
                }
            }

            map.EndConstruct();

            var translation = resultBuilder.ToString();
            return (translation, map);
        }

        private static void ApplyPolyphonicPhraseOverrides(
            string content,
            string[] resultList,
            PinyinConfiguration configuration)
        {
            for (var start = 0; start < content.Length; start++)
            {
                var longestCandidate = Math.Min(configuration.MaxPolyphonicPhraseLength, content.Length - start);
                for (var length = longestCandidate; length > 1; length--)
                {
                    var phrase = content.Substring(start, length);
                    if (!configuration.PolyphonicPhraseOverrides.TryGetValue(phrase, out var pinyin) ||
                        pinyin.Length != length || start + length > resultList.Length)
                    {
                        continue;
                    }

                    for (var i = 0; i < length; i++)
                    {
                        resultList[start + i] = pinyin[i];
                    }

                    start += length - 1;
                    break;
                }
            }
        }

        /// <summary>
        /// Optimized Chinese character detection using the comprehensive CJK Unicode ranges
        /// </summary>
        private static bool ContainsChinese(ReadOnlySpan<char> text)
        {
            foreach (var c in text)
            {
                if (IsChineseCharacter(c))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Check if a character is a Chinese character using comprehensive Unicode ranges
        /// Covers CJK Unified Ideographs, Extension A
        /// </summary>
        private static bool IsChineseCharacter(char c)
        {
            return (c >= 0x4E00 && c <= 0x9FFF) ||     // CJK Unified Ideographs
                   (c >= 0x3400 && c <= 0x4DBF);       // CJK Extension A
        }

        private static string ToDoublePinyin(
            string fullPinyin,
            ReadOnlyDictionary<string, string> doublePinyinTable)
        {
            return doublePinyinTable.TryGetValue(fullPinyin, out var doublePinyinValue)
                ? doublePinyinValue
                : fullPinyin;
        }
    }
}
