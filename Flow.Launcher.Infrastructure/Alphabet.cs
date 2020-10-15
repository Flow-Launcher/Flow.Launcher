﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using hyjiacan.util.p4n;
using hyjiacan.util.p4n.format;
using JetBrains.Annotations;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using System.Threading.Tasks;

namespace Flow.Launcher.Infrastructure
{
    public interface IAlphabet
    {
        string Translate(string stringToTranslate);
    }

    public class Alphabet : IAlphabet
    {
        private readonly HanyuPinyinOutputFormat Format = new HanyuPinyinOutputFormat();
        private ConcurrentDictionary<string, string[][]> PinyinCache;
        private BinaryStorage<Dictionary<string, string[][]>> _pinyinStorage;
        private Settings _settings;

        public void Initialize([NotNull] Settings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Task.Run(() => InitializePinyinHelpers());
        }

        private void InitializePinyinHelpers()
        {
            Format.setToneType(HanyuPinyinToneType.WITHOUT_TONE);

            Stopwatch.Normal("|Flow Launcher.Infrastructure.Alphabet.Initialize|Preload pinyin cache", () =>
            {
                _pinyinStorage = new BinaryStorage<Dictionary<string, string[][]>>("Pinyin");

                var loaded = _pinyinStorage.TryLoad(new Dictionary<string, string[][]>());

                PinyinCache = new ConcurrentDictionary<string, string[][]>(loaded);

                // force pinyin library static constructor initialize
                PinyinHelper.toHanyuPinyinStringArray('T', Format);
            });
            Log.Info($"|Flow Launcher.Infrastructure.Alphabet.Initialize|Number of preload pinyin combination<{PinyinCache.Count}>");

        }

        public string Translate(string str)
        {
            return ConvertChineseCharactersToPinyin(str);
        }

        public string ConvertChineseCharactersToPinyin(string source)
        {
            if (!_settings.ShouldUsePinyin)
                return source;

            if (string.IsNullOrEmpty(source))
                return source;

            if (!ContainsChinese(source))
                return source;

            var combination = PinyinCombination(source);

            var pinyinArray = combination.Select(x => string.Join("", x));
            var acronymArray = combination.Select(Acronym).Distinct();

            var joinedSingleStringCombination = new StringBuilder();
            var all = acronymArray.Concat(pinyinArray);
            all.ToList().ForEach(x => joinedSingleStringCombination.Append(x));

            return joinedSingleStringCombination.ToString();
        }

        public void Save()
        {
            if (!_settings.ShouldUsePinyin)
            {
                return;
            }

            lock (_pinyinStorage)
            {
                _pinyinStorage.Save(PinyinCache.ToDictionary(i => i.Key, i => i.Value));
            }
        }

        private static string[] EmptyStringArray = new string[0];
        private static string[][] Empty2DStringArray = new string[0][];

        /// <summmary>
        /// replace chinese character with pinyin, non chinese character won't be modified
        /// Because we don't have words dictionary, so we can only return all possiblie pinyin combination
        /// e.g. 音乐 will return yinyue and yinle
        /// <param name="characters"> should be word or sentence, instead of single character. e.g. 微软 </param>
        /// </summmary>
        public string[][] PinyinCombination(string characters)
        {
            if (!_settings.ShouldUsePinyin || string.IsNullOrEmpty(characters))
            {
                return Empty2DStringArray;
            }

            if (!PinyinCache.ContainsKey(characters))
            {
                // var allPinyins = new List<string[]>();

                var allPinyins = characters.Select(c =>
                    PinyinHelper.toHanyuPinyinStringArray(c) switch
                    {
                        null => new string[] { c.ToString() },
                        string[] pinyins => pinyins.Distinct().ToArray()
                    }
                );

                var combination = allPinyins.Aggregate(Combination).Select(c => c.Split(';')).ToArray();
                PinyinCache[characters] = combination;
                return combination;
            }
            else
            {
                return PinyinCache[characters];
            }
        }

        public string Acronym(string[] pinyin)
        {
            var acronym = string.Concat(pinyin.Select(p => p[0]));
            return acronym;
        }

        public bool ContainsChinese(string word)
        {
            if (!_settings.ShouldUsePinyin)
            {
                return false;
            }

            if (word.Length > 40)
            {
                //Skip strings that are too long string for Pinyin conversion.
                return false;
            }

            var chinese = word.Select(PinyinHelper.toHanyuPinyinStringArray)
                              .Any(p => p != null);
            return chinese;
        }

        private string[] Combination(string[] array1, string[] array2)
        {
            if (!_settings.ShouldUsePinyin)
            {
                return EmptyStringArray;
            }

            var combination = (
                from a1 in array1
                from a2 in array2
                select $"{a1};{a2}"
            ).ToArray();
           return combination;
        }
    }
}
