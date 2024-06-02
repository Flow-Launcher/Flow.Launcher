using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Flow.Launcher.Infrastructure.UserSettings;
using ToolGood.Words.Pinyin;

namespace Flow.Launcher.Infrastructure
{
    public class PinyinAlphabet : IAlphabet
    {
        private ConcurrentDictionary<string, (string translation, TranslationMapping map)> _pinyinCache =
            new ConcurrentDictionary<string, (string translation, TranslationMapping map)>();

        private Settings _settings;

        public void Initialize([NotNull] Settings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public bool ShouldTranslate(string stringToTranslate)
        {
            return WordsHelper.HasChinese(stringToTranslate);
        }

        public (string translation, TranslationMapping map) Translate(string content)
        {
            if (_settings.ShouldUsePinyin)
            {
                if (!_pinyinCache.ContainsKey(content))
                {
                    return BuildCacheFromContent(content);
                }
                else
                {
                    return _pinyinCache[content];
                }
            }
            return (content, null);
        }

        private (string translation, TranslationMapping map) BuildCacheFromContent(string content)
        {
            if (WordsHelper.HasChinese(content))
            {
                var resultList = WordsHelper.GetPinyinList(content);

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

                return _pinyinCache[content] = (key, map);
            }
            else
            {
                return (content, null);
            }
        }
    }
}
