using System;
using System.Collections.Concurrent;
using System.Text;
using JetBrains.Annotations;
using Flow.Launcher.Infrastructure.UserSettings;
using ToolGood.Words.Pinyin;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Flow.Launcher.Infrastructure
{
    public class PinyinAlphabet : IAlphabet
    {
        private readonly ConcurrentDictionary<string, (string translation, TranslationMapping map)> _pinyinCache =
            new();

        private Settings _settings;

        public void Initialize([NotNull] Settings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public bool ShouldTranslate(string stringToTranslate)
        {
            return _settings.UseDoublePinyin ?
                (!WordsHelper.HasChinese(stringToTranslate) && stringToTranslate.Length % 2 == 0) :
                !WordsHelper.HasChinese(stringToTranslate);
        }

        public (string translation, TranslationMapping map) Translate(string content)
        {
            if (_settings.ShouldUsePinyin)
            {
                if (!_pinyinCache.TryGetValue(content, out var value))
                {
                    return BuildCacheFromContent(content);
                }
                else
                {
                    return value;
                }
            }
            return (content, null);
        }

        private (string translation, TranslationMapping map) BuildCacheFromContent(string content)
        {
            if (!WordsHelper.HasChinese(content))
            {
                return (content, null);
            }

            var resultList = WordsHelper.GetPinyinList(content);

            StringBuilder resultBuilder = new StringBuilder();
            TranslationMapping map = new TranslationMapping();

            bool pre = false;

            for (int i = 0; i < resultList.Length; i++)
            {
                if (content[i] >= 0x3400 && content[i] <= 0x9FD5)
                {
                    string dp = _settings.UseDoublePinyin ? ToDoublePin(resultList[i]) : resultList[i];
                    map.AddNewIndex(i, resultBuilder.Length, dp.Length + 1);
                    resultBuilder.Append(' ');
                    resultBuilder.Append(dp);
                    pre = true;
                }
                else
                {
                    if (pre)
                    {
                        pre = false;
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

        private static readonly ReadOnlyDictionary<string, string> special = new(new Dictionary<string, string>(){
            {"A", "aa"},
            {"Ai", "ai"},
            {"An", "an"},
            {"Ang", "ah"},
            {"Ao", "ao"},
            {"E", "ee"},
            {"Ei", "ei"},
            {"En", "en"},
            {"Er", "er"},
            {"O", "oo"},
            {"Ou", "ou"}
        });


        private static readonly ReadOnlyDictionary<string, string> first = new(new Dictionary<string, string>(){
            {"Ch", "i"},
            {"Sh", "u"},
            {"Zh", "v"}
        });


        private static readonly ReadOnlyDictionary<string, string> second = new(new Dictionary<string, string>()
        {
            {"ua", "x"},
            {"ei", "w"},
            {"e", "e"},
            {"ou", "z"},
            {"iu", "q"},
            {"ve", "t"},
            {"ue", "t"},
            {"u", "u"},
            {"i", "i"},
            {"o", "o"},
            {"uo", "o"},
            {"ie", "p"},
            {"a", "a"},
            {"ong", "s"},
            {"iong", "s"},
            {"ai", "d"},
            {"ing", "k"},
            {"uai", "k"},
            {"ang", "h"},
            {"uan", "r"},
            {"an", "j"},
            {"en", "f"},
            {"ia", "x"},
            {"iang", "l"},
            {"uang", "l"},
            {"eng", "g"},
            {"in", "b"},
            {"ao", "c"},
            {"v", "v"},
            {"ui", "v"},
            {"un", "y"},
            {"iao", "n"},
            {"ian", "m"}
        });

        private static string ToDoublePin(string fullPinyin)
        {
            // Assuming s is valid
            StringBuilder doublePin = new StringBuilder();

            if (fullPinyin.Length <= 3 && (fullPinyin[0] == 'a' || fullPinyin[0] == 'e' || fullPinyin[0] == 'o'))
            {
                if (special.TryGetValue(fullPinyin, out var value))
                {
                    return value;
                }
            }

            // zh, ch, sh
            if (fullPinyin.Length >= 2 && first.ContainsKey(fullPinyin[..2]))
            {
                doublePin.Append(first[fullPinyin[..2]]);

                if (second.TryGetValue(fullPinyin[2..], out string tmp))
                {
                    doublePin.Append(tmp);
                }
                else
                {
                    doublePin.Append(fullPinyin[2..]);
                }
            }
            else
            {
                doublePin.Append(fullPinyin[0]);

                if (second.TryGetValue(fullPinyin[1..], out string tmp))
                {
                    doublePin.Append(tmp);
                }
                else
                {
                    doublePin.Append(fullPinyin[1..]);
                }
            }

            return doublePin.ToString();
        }
        #endregion
    }
}
