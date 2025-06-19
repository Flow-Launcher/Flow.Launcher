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
            Dictionary<string, Dictionary<string, string>> table = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(jsonStream);
            if (!table.TryGetValue(_settings.DoublePinyinSchema, out var value))
            {
                throw new InvalidOperationException("DoublePinyinSchema is invalid or double pinyin table is broken.");
            }
            currentDoublePinyinTable = new ReadOnlyDictionary<string, string>(value);
        }

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

        public bool ShouldTranslate(string stringToTranslate)
        {
            return _settings.UseDoublePinyin ?
                (!WordsHelper.HasChinese(stringToTranslate) && stringToTranslate.Length % 2 == 0) :
                !WordsHelper.HasChinese(stringToTranslate);
        }

        public (string translation, TranslationMapping map) Translate(string content)
        {
            if (!_settings.ShouldUsePinyin || !WordsHelper.HasChinese(content))
                return (content, null);

            return _pinyinCache.TryGetValue(content, out var value)
                ? value
                : BuildCacheFromContent(content);
        }

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
                        resultBuilder.Append(' ');
                        map.AddNewIndex(i, resultBuilder.Length, translated.Length + 1);
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
