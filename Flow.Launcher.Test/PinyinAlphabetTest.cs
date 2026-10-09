using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Threading;
using System.Threading.Tasks;
using ToolGood.Words.Pinyin;

namespace Flow.Launcher.Test
{
    [TestFixture]
    public class PinyinAlphabetTest
    {
        [TestCase("重启", "Chong Qi")]
        [TestCase("重启 Flow Launcher", "Chong Qi Flow Launcher")]
        [TestCase("重庆", "Chong Qing")]
        [TestCase("核查", "He Cha")]
        public void Translate_ShouldUseExpectedPinyinForPolyphonicPhrases(string content, string expected)
        {
            var alphabet = new PinyinAlphabet(new Settings
            {
                ShouldUsePinyin = true,
                UsePolyphonicPhraseOverrides = true
            });

            var result = alphabet.Translate(content);

            ClassicAssert.AreEqual(expected, result.translation);
        }

        [Test]
        public void FuzzyMatch_ShouldMatchRestartByCorrectPinyin()
        {
            var settings = new Settings
            {
                ShouldUsePinyin = true,
                UsePolyphonicPhraseOverrides = true
            };
            var matcher = new StringMatcher(new PinyinAlphabet(settings), settings);

            var result = matcher.FuzzyMatch("chongqi", "重启");

            ClassicAssert.True(result.Success);
        }

        [Test]
        public void Translate_WhenPolyphonicPhraseOverridesDisabled_ShouldUseLibraryPinyin()
        {
            var alphabet = new PinyinAlphabet(new Settings
            {
                ShouldUsePinyin = true,
                UsePolyphonicPhraseOverrides = false
            });

            var (translation, _) = alphabet.Translate("重启");

            ClassicAssert.AreEqual("Zhong Qi", translation);
        }

        [Test]
        public void Translate_WhenPolyphonicPhraseOverridesAreDisabledAfterCaching_ShouldInvalidateCache()
        {
            var settings = new Settings
            {
                ShouldUsePinyin = true,
                UsePolyphonicPhraseOverrides = true
            };
            var alphabet = new PinyinAlphabet(settings);
            ClassicAssert.AreEqual("Chong Qi", alphabet.Translate("重启").translation);

            settings.UsePolyphonicPhraseOverrides = false;

            ClassicAssert.AreEqual("Zhong Qi", alphabet.Translate("重启").translation);
        }

        [Test]
        public void Translate_WithDoublePinyinAndPolyphonicPhraseOverrides_ShouldUseOverrideBeforeDoublePinyinConversion()
        {
            var alphabet = new PinyinAlphabet(new Settings
            {
                ShouldUsePinyin = true,
                UseDoublePinyin = true,
                UsePolyphonicPhraseOverrides = true,
                DoublePinyinSchema = DoublePinyinSchemas.XiaoHe
            });

            var result = alphabet.Translate("重启");

            ClassicAssert.AreEqual("is qi", result.translation);
        }

        [Test]
        public void Translate_WhenDoublePinyinIsEnabledAfterCaching_ShouldKeepPolyphonicPhraseOverride()
        {
            var settings = new Settings
            {
                ShouldUsePinyin = true,
                UsePolyphonicPhraseOverrides = true,
                DoublePinyinSchema = DoublePinyinSchemas.XiaoHe
            };
            var alphabet = new PinyinAlphabet(settings);
            ClassicAssert.AreEqual("Chong Qi", alphabet.Translate("重启").translation);

            settings.UseDoublePinyin = true;

            ClassicAssert.AreEqual("is qi", alphabet.Translate("重启").translation);
        }

        [Test]
        public void FuzzyMatch_WithDoublePinyinAndPolyphonicPhraseOverrides_ShouldMatchOverridePronunciation()
        {
            var settings = new Settings
            {
                ShouldUsePinyin = true,
                UseDoublePinyin = true,
                UsePolyphonicPhraseOverrides = true,
                DoublePinyinSchema = DoublePinyinSchemas.XiaoHe
            };
            var matcher = new StringMatcher(new PinyinAlphabet(settings), settings);

            var overridePronunciation = matcher.FuzzyMatch("isqi", "重启");
            var libraryPronunciation = matcher.FuzzyMatch("vsqi", "重启");

            ClassicAssert.True(overridePronunciation.Success);
            ClassicAssert.False(libraryPronunciation.Success);
        }

        [Test]
        public async Task Translate_WhenConfigurationChangesDuringBuild_ShouldNotPublishStaleResultAsync()
        {
            var settings = new Settings
            {
                ShouldUsePinyin = true,
                UsePolyphonicPhraseOverrides = true
            };
            using var oldConfigurationCaptured = new ManualResetEventSlim();
            using var continueBuild = new ManualResetEventSlim();
            var alphabet = new PinyinAlphabet(settings, content =>
            {
                oldConfigurationCaptured.Set();
                continueBuild.Wait();
                return WordsHelper.GetPinyinList(content);
            });

            var translationTask = Task.Run(() => alphabet.Translate("重启").translation);
            try
            {
                ClassicAssert.True(oldConfigurationCaptured.Wait(5000), "The search worker did not start in time.");

                settings.UsePolyphonicPhraseOverrides = false;
                continueBuild.Set();

                ClassicAssert.AreEqual("Zhong Qi", await translationTask);
                ClassicAssert.AreEqual("Zhong Qi", alphabet.Translate("重启").translation);
            }
            finally
            {
                continueBuild.Set();
            }
        }
    }
}
