using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Flow.Launcher.Infrastructure.UserSettings;
using ToolGood.Words.Pinyin;

namespace Flow.Launcher.Infrastructure
{
    public class DoublePinAlphabet : IAlphabet
    {
        private ConcurrentDictionary<string, (string translation, TranslationMapping map)> _doublePinCache =
            new ConcurrentDictionary<string, (string translation, TranslationMapping map)>();

        private Settings _settings;

        public void Initialize([NotNull] Settings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public bool CanBeTranslated(string stringToTranslate)
        {
            return WordsHelper.HasChinese(stringToTranslate);
        }

        public (string translation, TranslationMapping map) Translate(string content)
        {
            if (_settings.ShouldUseDoublePin)
            {
                if (!_doublePinCache.ContainsKey(content))
                {
                    return BuildCacheFromContent(content);
                }
                else
                {
                    return _doublePinCache[content];
                }
            }
            return (content, null);
        }

        private (string translation, TranslationMapping map) BuildCacheFromContent(string content)
        {
            if (WordsHelper.HasChinese(content))
            {
                var resultList = WordsHelper.GetPinyinList(content).Select(ToDoublePin).ToArray();
                StringBuilder resultBuilder = new StringBuilder();
                TranslationMapping map = new TranslationMapping();

                bool pre = false;

                for (int i = 0; i < resultList.Length; i++)
                {
                    if (content[i] >= 0x3400 && content[i] <= 0x9FD5)
                    {
                        map.AddNewIndex(i, resultBuilder.Length, resultList[i].Length + 1);
                        resultBuilder.Append(' ');
                        resultBuilder.Append(resultList[i]);
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
                map.setKey(key);

                return _doublePinCache[content] = (key, map);
            }
            else
            {
                return (content, null);
            }
        }

        private static readonly ReadOnlyDictionary<string, string> special = new(new Dictionary<string, string>(){
            {"a", "aa"},
            {"ai", "ai"},
            {"an", "an"},
            {"ang", "ah"},
            {"ao", "ao"},
            {"e", "ee"},
            {"ei", "ei"},
            {"en", "en"},
            {"er", "er"},
            {"o", "oo"},
            {"ou", "ou"}
        });


        private static readonly ReadOnlyDictionary<string, string> first = new(new Dictionary<string, string>(){
            {"ch", "i"},
            {"sh", "u"},
            {"zh", "v"}
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
                if (special.ContainsKey(fullPinyin))
                {
                    return special[fullPinyin];
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
    }
}
