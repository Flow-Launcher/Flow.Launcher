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
    public class TranslationMapping
    {
        private bool constructed;

        private List<int> originalIndexs = new List<int>();
        private List<int> translatedIndexs = new List<int>();
        private int translatedLength = 0;

        public string key { get; private set; }

        public void setKey(string key)
        {
            this.key = key;
        }

        public void AddNewIndex(int originalIndex, int translatedIndex, int length)
        {
            if (constructed)
                throw new InvalidOperationException("Mapping shouldn't be changed after constructed");

            originalIndexs.Add(originalIndex);
            translatedIndexs.Add(translatedIndex);
            translatedIndexs.Add(translatedIndex + length);
            translatedLength += length - 1;
        }

        public int MapToOriginalIndex(int translatedIndex)
        {
            if (translatedIndex > translatedIndexs.Last())
                return translatedIndex - translatedLength - 1;

            int lowerBound = 0;
            int upperBound = originalIndexs.Count - 1;

            int count = 0;

            // Corner case handle
            if (translatedIndex < translatedIndexs[0])
                return translatedIndex;
            if (translatedIndex > translatedIndexs.Last())
            {
                int indexDef = 0;
                for (int k = 0; k < originalIndexs.Count; k++)
                {
                    indexDef += translatedIndexs[k * 2 + 1] - translatedIndexs[k * 2];
                }

                return translatedIndex - indexDef - 1;
            }

            // Binary Search with Range
            for (int i = originalIndexs.Count / 2;; count++)
            {
                if (translatedIndex < translatedIndexs[i * 2])
                {
                    // move to lower middle
                    upperBound = i;
                    i = (i + lowerBound) / 2;
                }
                else if (translatedIndex > translatedIndexs[i * 2 + 1] - 1)
                {
                    lowerBound = i;
                    // move to upper middle
                    // due to floor of integer division, move one up on corner case
                    i = (i + upperBound + 1) / 2;
                }
                else
                    return originalIndexs[i];

                if (upperBound - lowerBound <= 1 &&
                    translatedIndex > translatedIndexs[lowerBound * 2 + 1] &&
                    translatedIndex < translatedIndexs[upperBound * 2])
                {
                    int indexDef = 0;

                    for (int j = 0; j < upperBound; j++)
                    {
                        indexDef += translatedIndexs[j * 2 + 1] - translatedIndexs[j * 2];
                    }

                    return translatedIndex - indexDef - 1;
                }
            }
        }

        public void endConstruct()
        {
            if (constructed)
                throw new InvalidOperationException("Mapping has already been constructed");
            constructed = true;
        }
    }

    /// <summary>
    /// Translate a language to English letters using a given rule.
    /// </summary>
    public interface IAlphabet
    {
        /// <summary>
        /// Translate a string to English letters, using a given rule.
        /// </summary>
        /// <param name="stringToTranslate">String to translate.</param>
        /// <returns></returns>
        public (string translation, TranslationMapping map) Translate(string stringToTranslate);

        /// <summary>
        /// Determine if a string can be translated to English letter with this Alphabet.
        /// </summary>
        /// <param name="stringToTranslate">String to translate.</param>
        /// <returns></returns>
        public bool CanBeTranslated(string stringToTranslate);
    }

    public class PinyinAlphabet : IAlphabet
    {
        private ConcurrentDictionary<string, (string translation, TranslationMapping map)> _pinyinCache =
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
