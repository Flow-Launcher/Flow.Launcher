using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.UserSettings;
using ToolGood.Words.Pinyin;

namespace Flow.Launcher.Infrastructure
{
    public class PinyinAlphabet : IAlphabet
    {
        private readonly ConcurrentDictionary<string, (string translation, TranslationMapping map)> _pinyinCache =
            new();

        private readonly Settings _settings;

        public PinyinAlphabet()
        {
            _settings = Ioc.Default.GetRequiredService<Settings>();
        }

        public bool ShouldTranslate(string stringToTranslate)
        {
            return _settings.UseDoublePinyin ?
                (!WordsHelper.HasChinese(stringToTranslate) && stringToTranslate.Length % 2 == 0) :
                !WordsHelper.HasChinese(stringToTranslate);
        }

        public (string translation, TranslationMapping map) Translate(string content)
        {
            if (!_settings.ShouldUsePinyin)
                return (content, null);

            return _pinyinCache.TryGetValue(content, out var value)
                ? value
                : BuildCacheFromContent(content);
        }

        private (string translation, TranslationMapping map) BuildCacheFromContent(string content)
        {
            if (!WordsHelper.HasChinese(content))
            {
                return (content, null);
            }

            var resultList = WordsHelper.GetPinyinList(content);

            var resultBuilder = new StringBuilder();
            var map = new TranslationMapping();

            var pre = false;

            for (var i = 0; i < resultList.Length; i++)
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
            var fullPinyinSpan = fullPinyin.AsSpan();
            var doublePin = new StringBuilder();

            // Handle special cases (a, o, e)
            if (fullPinyin.Length <= 3 && (fullPinyinSpan[0] == 'a' || fullPinyinSpan[0] == 'e' || fullPinyinSpan[0] == 'o'))
            {
                if (special.TryGetValue(fullPinyin, out var value))
                {
                    return value;
                }
            }

            // Check for initials that are two characters long (zh, ch, sh)
            if (fullPinyin.Length >= 2)
            {
                var firstTwoString = fullPinyinSpan[..2].ToString();
                if (first.TryGetValue(firstTwoString, out var firstTwoDoublePin))
                {
                    doublePin.Append(firstTwoDoublePin);

                    var lastTwo = fullPinyinSpan[2..];
                    var lastTwoString = lastTwo.ToString();
                    if (second.TryGetValue(lastTwoString, out var tmp))
                    {
                        doublePin.Append(tmp);
                    }
                    else
                    {
                        doublePin.Append(lastTwo); // Todo: original pinyin, remove this line if not needed
                    }
                }
                else
                {
                    // Handle single-character initials
                    doublePin.Append(fullPinyinSpan[0]);

                    var lastOne = fullPinyinSpan[1..];
                    var lastOneString = lastOne.ToString();
                    if (second.TryGetValue(lastOneString, out var tmp))
                    {
                        doublePin.Append(tmp);
                    }
                    else
                    {
                        doublePin.Append(lastOne);
                    }
                }
            }
            return doublePin.ToString();
        }

        #endregion
    }
}
