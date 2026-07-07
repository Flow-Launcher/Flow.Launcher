using System;
using Flow.Launcher.Helper;
using NUnit.Framework;

namespace Flow.Launcher.Test
{
    [TestFixture]
    public class DeepLinkTest
    {
        [Test]
        public void FromCommandLineArgs_QueryFlag_NormalizesToQueryUri()
        {
            var result = DeepLink.FromCommandLineArgs(new[] { "--query", "foo bar" }, true);
            Assert.That(result, Is.EqualTo("flow-launcher://query?q=foo%20bar"));
        }

        [Test]
        public void FromCommandLineArgs_SingleDashQueryFlag_NormalizesToQueryUri()
        {
            var result = DeepLink.FromCommandLineArgs(new[] { "-query", "foo" }, true);
            Assert.That(result, Is.EqualTo("flow-launcher://query?q=foo"));
        }

        [Test]
        public void FromCommandLineArgs_FlowPluginPath_NormalizesToInstallUri()
        {
            var path = OperatingSystem.IsWindows() ? @"C:\tmp\My Plugin.flowplugin" : "/tmp/My Plugin.flowplugin";
            var result = DeepLink.FromCommandLineArgs(new[] { path }, true);
            Assert.That(result, Does.StartWith("flow-launcher://plugin/install?path="));
            Assert.That(Uri.UnescapeDataString(result.Split("path=")[1]), Does.EndWith("My Plugin.flowplugin"));
        }

        [Test]
        public void FromCommandLineArgs_RawSchemeUri_PassedThroughWhenAllowed()
        {
            var result = DeepLink.FromCommandLineArgs(new[] { "flow-launcher://settings" }, true);
            Assert.That(result, Is.EqualTo("flow-launcher://settings"));
        }

        [Test]
        public void FromCommandLineArgs_RawSchemeUri_DroppedWhenDisallowed()
        {
            var result = DeepLink.FromCommandLineArgs(new[] { "flow-launcher://settings" }, false);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void FromCommandLineArgs_FlowPluginPath_StillWorksWhenSchemeDisallowed()
        {
            var result = DeepLink.FromCommandLineArgs(new[] { "plugin.flowplugin" }, false);
            Assert.That(result, Does.StartWith("flow-launcher://plugin/install?path="));
        }

        [Test]
        public void FromCommandLineArgs_NoRelevantArgs_ReturnsNull()
        {
            Assert.That(DeepLink.FromCommandLineArgs(Array.Empty<string>(), true), Is.Null);
            Assert.That(DeepLink.FromCommandLineArgs(new[] { "--unrelated" }, true), Is.Null);
            Assert.That(DeepLink.FromCommandLineArgs(new[] { "--query" }, true), Is.Null); // flag without value
        }

        [TestCase("flow-launcher://query?q=hello%20world", "query", "q", "hello world")]
        [TestCase("flow-launcher://plugin/install?id=abc123", "plugin/install", "id", "abc123")]
        [TestCase("FLOW-LAUNCHER://SETTINGS", "settings", null, null)]
        public void TryParse_ValidUris_ExtractsVerbAndParameters(string payload, string expectedVerb, string paramKey, string paramValue)
        {
            var ok = DeepLink.TryParse(payload, out var verb, out var parameters);
            Assert.That(ok, Is.True);
            Assert.That(verb, Is.EqualTo(expectedVerb));
            if (paramKey != null)
                Assert.That(parameters[paramKey], Is.EqualTo(paramValue));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("just a plain query string")]
        [TestCase("https://example.com/notourscheme")]
        public void TryParse_InvalidPayloads_ReturnsFalse(string payload)
        {
            Assert.That(DeepLink.TryParse(payload, out _, out _), Is.False);
        }
    }
}
