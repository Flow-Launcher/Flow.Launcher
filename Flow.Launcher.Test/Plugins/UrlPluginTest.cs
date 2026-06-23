using NUnit.Framework;
using Flow.Launcher.Plugin.Url;
using System.Reflection;

namespace Flow.Launcher.Test.Plugins
{
    [TestFixture]
    public class UrlPluginTest
    {
        private static Main plugin;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            var settingsProperty = typeof(Main).GetProperty("Settings", BindingFlags.NonPublic | BindingFlags.Static);
            settingsProperty?.SetValue(null, new Settings());

            plugin = new Main();
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
        [TestCase("LOCALHOST")]
        [TestCase("Http://Example.Com")]
        [TestCase("hTTps://ExAmPlE.CoM")]
        [TestCase("LocalHost")]
        [TestCase("example.com/path")]
        [TestCase("example.com/path/to/resource")]
        [TestCase("http://example.com/path")]
        [TestCase("https://example.com/path?query=1")]
        [TestCase("192.168.1.1/path/to/resource")]
        [TestCase("192.168.1.1/path/to/resource?query=1")]
        [TestCase("localhost:8080/api/endpoint")]
        [TestCase("http://localhost/path")]
        [TestCase("[::1]/path")]
        [TestCase("[2001:db8::1]/path?query=1")]
        [TestCase("192.168.1.1?query=value")]
        [TestCase("192.168.1.1#fragment")]
        [TestCase("localhost:8080?test=123")]
        [TestCase("example.com#fragment")]
        public void WhenValidUrlThenIsUrlReturnsTrue(string url)
        {
            Assert.That(plugin.IsURL(url), Is.True);
        }

        [TestCase("2001:db8::1/path")]
        [TestCase("wwww")]
        [TestCase("wwww.c")]
        [TestCase("not a url")]
        [TestCase("just text")]
        [TestCase("http://")]
        [TestCase("://example.com")]
        [TestCase("0.0.0.0")] // reserved default route address / IPAddress.Any
        [TestCase("256.1.1.1")] // Invalid IPv4
        [TestCase("example")] // No TLD
        [TestCase("example..com")]
        [TestCase("example .com")]
        [TestCase("..example.com")]
        [TestCase(".com")]
        [TestCase("http://.com")]
        [TestCase("2001:db8:::1")]
        public void WhenInvalidUrlThenIsUrlReturnsFalse(string url)
        {
            Assert.That(plugin.IsURL(url), Is.False);
        }
    }
}
