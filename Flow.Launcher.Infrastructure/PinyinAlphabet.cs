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
        private readonly ConcurrentDictionary<string, (string translation, TranslationMapping map)> _pinyinCache = new();
        private readonly Settings _settings;
        private ReadOnlyDictionary<string, string> currentDoublePinyinTable;

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

        public void Reload()
        {
            LoadDoublePinyinTable();
            _pinyinCache.Clear();
        }

        private void CreateDoublePinyinTableFromStream(Stream jsonStream)
        {
            var table = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(jsonStream) ?? 
                throw new InvalidOperationException("Failed to deserialize double pinyin table: result is null");

            var schemaKey = _settings.DoublePinyinSchema.ToString();
            if (!table.TryGetValue(schemaKey, out var schemaDict))
            {
                throw new ArgumentException($"DoublePinyinSchema '{schemaKey}' is invalid or double pinyin table is broken.");
            }

            currentDoublePinyinTable = new ReadOnlyDictionary<string, string>(schemaDict);
        }

        private void LoadDoublePinyinTable()
        {
            if (!_settings.UseDoublePinyin)
            {
                currentDoublePinyinTable = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
                return;
            }

            var tablePath = Path.Combine(AppContext.BaseDirectory, "Resources", "double_pinyin.json");
            try
            {
                using var fs = File.OpenRead(tablePath);
                CreateDoublePinyinTableFromStream(fs);
            }
            catch (FileNotFoundException e)
            {
                Log.Exception(nameof(PinyinAlphabet), $"Double pinyin table file not found: {tablePath}", e);
                currentDoublePinyinTable = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
            }
            catch (DirectoryNotFoundException e)
            {
                Log.Exception(nameof(PinyinAlphabet), $"Directory not found for double pinyin table: {tablePath}", e);
                currentDoublePinyinTable = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
            }
            catch (UnauthorizedAccessException e)
            {
                Log.Exception(nameof(PinyinAlphabet), $"Access denied to double pinyin table: {tablePath}", e);
                currentDoublePinyinTable = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
            }
            catch (System.Exception e)
            {
                Log.Exception(nameof(PinyinAlphabet), $"Failed to load double pinyin table from file: {tablePath}", e);
                currentDoublePinyinTable = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
            }
        }

        public bool ShouldTranslate(string stringToTranslate)
        {
            // If the query (stringToTranslate) does NOT contain Chinese characters, 
            // we should translate the target string to pinyin for matching
            return _settings.ShouldUsePinyin && !ContainsChinese(stringToTranslate);
        }

        public (string translation, TranslationMapping map) Translate(string content)
        {
            if (!_settings.ShouldUsePinyin || !ContainsChinese(content))
                return (content, null);

            return _pinyinCache.TryGetValue(content, out var cached) ? cached : BuildCacheFromContent(content);
        }

        private (string translation, TranslationMapping map) BuildCacheFromContent(string content)
        {
            var resultList = WordsHelper.GetPinyinList(content);
            var resultBuilder = new StringBuilder(_settings.UseDoublePinyin ? 3 : 4); // Pre-allocate with estimated capacity
            var map = new TranslationMapping();

            var previousIsChinese = false;

            for (var i = 0; i < resultList.Length; i++)
            {
                if (IsChineseCharacter(content[i]))
                {
                    var translated = _settings.UseDoublePinyin ? ToDoublePinyin(resultList[i]) : resultList[i];
                    
                    if (i > 0)
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
                        resultBuilder.Append(' ');
                    }
                    
                    map.AddNewIndex(resultBuilder.Length, resultList[i].Length);
                    resultBuilder.Append(resultList[i]);
                }
            }

            map.EndConstruct();

            var translation = resultBuilder.ToString();
            var result = (translation, map);
            
            return _pinyinCache[content] = result;
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

        private string ToDoublePinyin(string fullPinyin)
        {
            return currentDoublePinyinTable.TryGetValue(fullPinyin, out var doublePinyinValue) 
                ? doublePinyinValue 
                : fullPinyin;
        }
    }
}
