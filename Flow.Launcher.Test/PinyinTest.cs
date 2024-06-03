using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using NUnit.Framework;

namespace Flow.Launcher.Test;

[TestFixture]
internal class PinyinTest
{
    [Test]
    public void TestShouldTranslate()
    {
        var pinyinAlphabet = new PinyinAlphabet();
        var settings = new Settings();
        pinyinAlphabet.Initialize(settings);

        settings.ShouldUsePinyin = true;
        settings.UseDoublePinyin = false;

        Assert.IsTrue(pinyinAlphabet.ShouldTranslate("test"));
        Assert.IsFalse(pinyinAlphabet.ShouldTranslate("测试"));
        Assert.IsFalse(pinyinAlphabet.ShouldTranslate("测试test"));

        settings.UseDoublePinyin = true;
        settings.ShouldUsePinyin = true;

        Assert.IsTrue(pinyinAlphabet.ShouldTranslate("test"));
        Assert.IsFalse(pinyinAlphabet.ShouldTranslate("测试"));
        Assert.IsFalse(pinyinAlphabet.ShouldTranslate("测试test"));
    }

    [Test]
    public void TestTranslate()
    {
        var pinyinAlphabet = new PinyinAlphabet();
        var settings = new Settings();
        pinyinAlphabet.Initialize(settings);

        settings.ShouldUsePinyin = true;
        settings.UseDoublePinyin = false;

        var result = pinyinAlphabet.Translate("测试");
        Assert.AreEqual("Ce Shi", result.translation);
        Assert.AreEqual(result.map.MapToOriginalIndex(0), 0);
        Assert.AreEqual(result.map.MapToOriginalIndex(1), 0);
        Assert.AreEqual(result.map.MapToOriginalIndex(3), 1);

        result = pinyinAlphabet.Translate("test");
        Assert.AreEqual("test", result.translation);
        Assert.IsNull(result.map);

        result = pinyinAlphabet.Translate("test测试你");
        Assert.AreEqual("test Ce Shi Ni", result.translation);
        Assert.AreEqual(result.map.MapToOriginalIndex(0), 0);
        Assert.AreEqual(result.map.MapToOriginalIndex(2), 1);
        Assert.AreEqual(result.map.MapToOriginalIndex(5), 4);
        Assert.AreEqual(result.map.MapToOriginalIndex(8), 5);
        Assert.AreEqual(result.map.MapToOriginalIndex(14), 6);
    }
}
