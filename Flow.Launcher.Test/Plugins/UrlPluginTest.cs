using System;
using System.Reflection;
using Flow.Launcher.Plugin.Url;
using NUnit.Framework;

namespace Flow.Launcher.Test.Plugins
{
    [TestFixture]
    public class UrlPluginTest
    {
        private Settings _settings = new()
        {
            AlwaysOpenWithHttps = false
        };

        private readonly Main _plugin = new();

        public UrlPluginTest()
        {
            // Set the static Settings property before running tests
            var settingProperty = typeof(Main).GetProperty("Settings", BindingFlags.NonPublic | BindingFlags.Static);
            if (settingProperty == null)
                Assert.Fail("Could not find property 'Settings' on Flow.Launcher.Plugin.Url.Main");
            settingProperty.SetValue(null, _settings);
        }

        [TestCase("http://www.google.com")]
        [TestCase("https://www.google.com")]
        [TestCase("http://google.com")]
        [TestCase("ftp://google.com")]
        [TestCase("www.google.com")]
        [TestCase("google.com")]
        [TestCase("http://localhost")]
        [TestCase("https://localhost")]
        [TestCase("http://localhost:80")]
        [TestCase("https://localhost:80")]
        [TestCase("localhost")]
        [TestCase("localhost:8080")]
        [TestCase("http://110.10.10.10")]
        [TestCase("110.10.10.10")]
        [TestCase("110.10.10.10:8080")]
        [TestCase("192.168.1.1")]
        [TestCase("root@192.168.1.1")]
        [TestCase("root@192.168.1.1:1080")]
        [TestCase("root:password@127.0.0.1:1080")]
        [TestCase("192.168.1.1:3000")]
        [TestCase("ftp://110.10.10.10")]
        [TestCase("[2001:db8::1]")]
        [TestCase("[2001:db8::1]:8080")]
        [TestCase("http://[2001:db8::1]")]
        [TestCase("https://[2001:db8::1]:8080")]
        [TestCase("[::1]")]
        [TestCase("[::1]:8080")]
        [TestCase("2001:db8::1")]
        [TestCase("fe80:1:2::3:4")]
        [TestCase("::1")]
        [TestCase("HTTP://EXAMPLE.COM")]
        [TestCase("HTTPS://EXAMPLE.COM")]
        [TestCase("EXAMPLE.COM")]
        [TestCase("EXAMPLE.COM/index.html")]
        [TestCase("LOCALHOST")]
        public void WhenValidUrlThenIsUrlReturnsTrue(string url)
        {

            Assert.That(_plugin.IsURL(url), Is.True);
        }

        [TestCase("wwww")]
        [TestCase("wwww.c")]
        [TestCase("not a url")]
        [TestCase("just text")]
        [TestCase("http://")]
        [TestCase("://example.com")]
        [TestCase("0.0.0.0")] // Pattern excludes 0.0.0.0
        [TestCase("256.1.1.1")] // Invalid IPv4
        [TestCase("example")] // No TLD
        [TestCase(".com")]
        [TestCase("http://.com")]
        public void WhenInvalidUrlThenIsUrlReturnsFalse(string url)
        {
            Assert.That(_plugin.IsURL(url), Is.False);
        }
    }
}
