using NUnit.Framework;
using NUnit.Framework.Legacy;
using Flow.Launcher.Plugin.Url;

namespace Flow.Launcher.Test.Plugins
{
    [TestFixture]
    public class UrlPluginTest
    {
        [Test]
        public void URLMatchTest()
        {
            var plugin = new Main();
            ClassicAssert.IsTrue(plugin.IsURL("http://www.google.com"));
            ClassicAssert.IsTrue(plugin.IsURL("https://www.google.com"));
            ClassicAssert.IsTrue(plugin.IsURL("http://google.com"));
            ClassicAssert.IsTrue(plugin.IsURL("www.google.com"));
            ClassicAssert.IsTrue(plugin.IsURL("google.com"));
            ClassicAssert.IsTrue(plugin.IsURL("http://localhost"));
            ClassicAssert.IsTrue(plugin.IsURL("https://localhost"));
            ClassicAssert.IsTrue(plugin.IsURL("http://localhost:80"));
            ClassicAssert.IsTrue(plugin.IsURL("https://localhost:80"));
            ClassicAssert.IsTrue(plugin.IsURL("http://110.10.10.10"));
            ClassicAssert.IsTrue(plugin.IsURL("110.10.10.10"));
            ClassicAssert.IsTrue(plugin.IsURL("ftp://110.10.10.10"));


            ClassicAssert.IsFalse(plugin.IsURL("wwww"));
            ClassicAssert.IsFalse(plugin.IsURL("wwww.c"));
            ClassicAssert.IsFalse(plugin.IsURL("wwww.c"));
        }
    }
}
