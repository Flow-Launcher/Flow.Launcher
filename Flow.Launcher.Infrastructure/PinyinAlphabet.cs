using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using ToolGood.Words.Pinyin;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Localization;

namespace Flow.Launcher.Infrastructure
{
    public interface IAlphabet
    {
        string Translate(string stringToTranslate);
    }

    public class PinyinAlphabet : IAlphabet
    {
        private ConcurrentDictionary<string, string> _pinyinCache = new ConcurrentDictionary<string, string>();
        private Settings _settings;

        public void Initialize([NotNull] Settings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }


        public string Translate(string content)
        {
            if (_settings.ShouldUsePinyin)
            {
                if (!_pinyinCache.ContainsKey(content))
                {
                    if (WordsHelper.HasChinese(content))
                    {
                        var resultList = WordsHelper.GetPinyinList(content);

                        List<int> chineseIndexs = new List<int>();

                        for (int i = 0; i < content.Length; i++)
                        {
                            if (resultList[i].Length != 1 || !(resultList[i][0] == content[i]))
                                chineseIndexs.Add(i);
                        }
                        StringBuilder resultBuilder = new StringBuilder();
                        resultBuilder.Append(string.Concat(resultList.Where((r, i) => chineseIndexs.Contains(i)).Select(s => s.First())));
                        resultBuilder.Append(' ');

                        int currentChineseIndex = 0;
                        int lastChineseIndex = -1;
                        for (int i = 0; i < resultList.Length; i++)
                        {
                            if (currentChineseIndex < chineseIndexs.Count && chineseIndexs[currentChineseIndex] == i)
                            {
                                resultBuilder.Append(' ');

                                resultBuilder.Append(resultList[i]);
                                currentChineseIndex++;
                                lastChineseIndex = i;
                            }
                            else
                            {
                                if (i == lastChineseIndex + 1)
                                {
                                    resultBuilder.Append(' ');
                                }
                                resultBuilder.Append(resultList[i]);
                            }
                        }


                        return _pinyinCache[content] = resultBuilder.ToString();
                    }
                    else
                    {
                        return content;
                    }
                }
                else
                {
                    return _pinyinCache[content];
                }
            }
            else
            {
                return content;
            }
        }

        private string GetFirstPinyinChar(string content)
        {
            return string.Concat(content.Split(' ').Select(x => x.First()));
        }
    }
}