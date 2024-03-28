using NUnit.Framework;
using Flow.Launcher.Plugin.Url;

namespace Flow.Launcher.Test
{
    [TestFixture]
    public class UrlPluginTest
    {
        [Test]
        public void URLMatchTest()
        {
            var plugin = new Main();
            Assert.That(plugin.IsURL("http://www.google.com"), Is.True);
            Assert.That(plugin.IsURL("https://www.google.com"), Is.True);
            Assert.That(plugin.IsURL("http://google.com"), Is.True);
            Assert.That(plugin.IsURL("www.google.com"), Is.True);
            Assert.That(plugin.IsURL("google.com"), Is.True);
            Assert.That(plugin.IsURL("http://localhost"), Is.True);
            Assert.That(plugin.IsURL("https://localhost"), Is.True);
            Assert.That(plugin.IsURL("http://localhost:80"), Is.True);
            Assert.That(plugin.IsURL("https://localhost:80"), Is.True);
            Assert.That(plugin.IsURL("http://110.10.10.10"), Is.True);
            Assert.That(plugin.IsURL("110.10.10.10"), Is.True);
            Assert.That(plugin.IsURL("ftp://110.10.10.10"), Is.True);


            Assert.That(plugin.IsURL("wwww"), Is.False);
            Assert.That(plugin.IsURL("wwww.c"), Is.False);
            Assert.That(plugin.IsURL("wwww.c"), Is.False);
        }
    }
}
