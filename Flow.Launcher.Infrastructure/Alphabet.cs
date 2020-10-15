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

namespace Flow.Launcher.Infrastructure
{
    public interface IAlphabet
    {
        string Translate(string stringToTranslate);
    }

    public class Alphabet : IAlphabet
    {
        private ConcurrentDictionary<string, string> _pinyinCache;
        private Settings _settings;

        public void Initialize([NotNull] Settings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }


        public string Translate(string content)
        {
            if (_settings.ShouldUsePinyin)
            {
                string result = _pinyinCache.GetValueOrDefault(content);
                if (result == null)
                {
                    if (WordsHelper.HasChinese(content))
                    {
                        result = WordsHelper.GetPinyin(content,";");
                        result = GetFirstPinyinChar(result) + result.Replace(";","");
                        _pinyinCache[content] = result;
                    }
                    else
                    {
                        result = content;
                    }
                }
                return result;
            }
            else
            {
                return content;
            }
        }

        private string GetFirstPinyinChar(string content)
        {
            return new string(content.Split(";").Select(c => c.First()).ToArray());
        }
    }
}