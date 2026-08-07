using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test
{
    [TestFixture]
    public class PinyinAlphabetTest
    {
        [TestCase("重启", "Chong Qi")]
        [TestCase("重启 Flow Launcher", "Chong Qi Flow Launcher")]
        [TestCase("重庆", "Chong Qing")]
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
    }
}
