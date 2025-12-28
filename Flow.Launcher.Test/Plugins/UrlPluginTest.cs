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
            ClassicAssert.IsTrue(plugin.IsURL("ftp://google.com"));
            ClassicAssert.IsTrue(plugin.IsURL("www.google.com"));
            ClassicAssert.IsTrue(plugin.IsURL("google.com"));
            ClassicAssert.IsTrue(plugin.IsURL("http://localhost"));
            ClassicAssert.IsTrue(plugin.IsURL("https://localhost"));
            ClassicAssert.IsTrue(plugin.IsURL("http://localhost:80"));
            ClassicAssert.IsTrue(plugin.IsURL("https://localhost:80"));
            ClassicAssert.IsTrue(plugin.IsURL("localhost"));
            ClassicAssert.IsTrue(plugin.IsURL("localhost:8080"));
            ClassicAssert.IsTrue(plugin.IsURL("http://110.10.10.10"));
            ClassicAssert.IsTrue(plugin.IsURL("110.10.10.10"));
            ClassicAssert.IsTrue(plugin.IsURL("110.10.10.10:8080"));
            ClassicAssert.IsTrue(plugin.IsURL("192.168.1.1"));
            ClassicAssert.IsTrue(plugin.IsURL("192.168.1.1:3000"));
            ClassicAssert.IsTrue(plugin.IsURL("ftp://110.10.10.10"));
            ClassicAssert.IsTrue(plugin.IsURL("[2001:db8::1]"));
            ClassicAssert.IsTrue(plugin.IsURL("[2001:db8::1]:8080"));
            ClassicAssert.IsTrue(plugin.IsURL("http://[2001:db8::1]"));
            ClassicAssert.IsTrue(plugin.IsURL("https://[2001:db8::1]:8080"));
            ClassicAssert.IsTrue(plugin.IsURL("[::1]"));
            ClassicAssert.IsTrue(plugin.IsURL("[::1]:8080"));
            ClassicAssert.IsTrue(plugin.IsURL("2001:db8::1"));
            ClassicAssert.IsTrue(plugin.IsURL("fe80:1:2::3:4"));
            ClassicAssert.IsTrue(plugin.IsURL("::1"));
            ClassicAssert.IsTrue(plugin.IsURL("HTTP://EXAMPLE.COM"));
            ClassicAssert.IsTrue(plugin.IsURL("HTTPS://EXAMPLE.COM"));
            ClassicAssert.IsTrue(plugin.IsURL("EXAMPLE.COM"));
            ClassicAssert.IsTrue(plugin.IsURL("LOCALHOST"));


            ClassicAssert.IsFalse(plugin.IsURL("wwww"));
            ClassicAssert.IsFalse(plugin.IsURL("wwww.c"));
            ClassicAssert.IsFalse(plugin.IsURL("wwww.c"));
            ClassicAssert.IsFalse(plugin.IsURL("not a url"));
            ClassicAssert.IsFalse(plugin.IsURL("just text"));
            ClassicAssert.IsFalse(plugin.IsURL("http://"));
            ClassicAssert.IsFalse(plugin.IsURL("://example.com"));
            ClassicAssert.IsFalse(plugin.IsURL("0.0.0.0")); // Pattern excludes 0.0.0.0
            ClassicAssert.IsFalse(plugin.IsURL("256.1.1.1")); // Invalid IPv4
            ClassicAssert.IsFalse(plugin.IsURL("example")); // No TLD
            ClassicAssert.IsFalse(plugin.IsURL(".com"));
            ClassicAssert.IsFalse(plugin.IsURL("http://.com"));
        }
    }
}
