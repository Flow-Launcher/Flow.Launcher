using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Flow.Launcher.Infrastructure.UserSettings;
using Microsoft.AspNetCore.Localization;
using ToolGood.Words.Pinyin;

namespace Flow.Launcher.Infrastructure
{
    public class TranslationMapping
    {
        private bool constructed;

        private List<int> originalIndexs = new List<int>();
        private List<int> translatedIndexs = new List<int>();
        private int translaedLength = 0;

        public void AddNewIndex(int originalIndex, int translatedIndex, int length)
        {
            if (constructed)
                throw new InvalidOperationException("Mapping shouldn't be changed after constructed");

            originalIndexs.Add(originalIndex);
            translatedIndexs.Add(translatedIndex);
            translatedIndexs.Add(translatedIndex + length);
            translaedLength += length - 1;
        }

        public int? MapToOriginalIndex(int translatedIndex)
        {
            if (translatedIndex > translatedIndexs.Last())
                return translatedIndex - translaedLength - 1;
            
            for (var i = 0; i < originalIndexs.Count; i++)
            {
                if (translatedIndex >= translatedIndexs[i * 2] && translatedIndex < translatedIndexs[i * 2 + 1])
                    return originalIndexs[i];
                if (translatedIndex < translatedIndexs[i * 2])
                {
                    int indexDiff = 0;
                    for (int j = 0; j < i; j++)
                    {
                        indexDiff += translatedIndexs[i * 2 + 1] - translatedIndexs[i * 2] - 1;
                    }

                    return translatedIndex - indexDiff;
                }
            }

            return translatedIndex;
        }

        public void endConstruct()
        {
            if (constructed)
                throw new InvalidOperationException("Mapping has already been constructed");
            constructed = true;
        }
    }

    public interface IAlphabet
    {
        public (string translation, TranslationMapping map) Translate(string stringToTranslate);
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

        public (string translation, TranslationMapping map) Translate(string content)
        {
            if (_settings.ShouldUsePinyin)
            {
                if (!_pinyinCache.ContainsKey(content))
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

                        return _pinyinCache[content] = (resultBuilder.ToString(), map);
                    }
                    else
                    {
                        return (content, null);
                    }
                }
                else
                {
                    return _pinyinCache[content];
                }
            }
            else
            {
                return (content, null);
            }
        }
    }
}