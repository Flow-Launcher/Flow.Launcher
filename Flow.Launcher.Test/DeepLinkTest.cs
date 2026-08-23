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
            var result = DeepLink.FromCommandLineArgs(new[] { "-q", "foo" }, true);
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

        [Test]
        public void FromCommandLineArgs_QueryFlagWithEmptyValue_ReturnsNull()
        {
            Assert.That(DeepLink.FromCommandLineArgs(new[] { "--query", "" }, true), Is.Null);
            Assert.That(DeepLink.FromCommandLineArgs(new[] { "-q", "" }, true), Is.Null);
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

        [TestCase("plugin.flowplugin", null, null)]
        [TestCase(null, "abc123", null)]
        [TestCase(null, null, "https://example.com/plugin.zip")]
        public void HasExactlyOneInstallIdentifier_SingleIdentifier_ReturnsTrue(string path, string id, string url)
        {
            Assert.That(DeepLink.HasExactlyOneInstallIdentifier(path, id, url), Is.True);
        }

        [TestCase(null, null, null)]     // none
        [TestCase("", "", "")]           // none (empty counts as absent)
        [TestCase("plugin.flowplugin", "abc123", null)]                       // path + id
        [TestCase(null, "abc123", "https://example.com/plugin.zip")]          // id + url
        [TestCase("plugin.flowplugin", "abc123", "https://example.com/x.zip")] // all three
        public void HasExactlyOneInstallIdentifier_NoneOrMultiple_ReturnsFalse(string path, string id, string url)
        {
            Assert.That(DeepLink.HasExactlyOneInstallIdentifier(path, id, url), Is.False);
        }

        [TestCase("flow-launcher://settings", "settings")]
        [TestCase("flow-launcher://settings/general", "settings/general")]
        [TestCase("flow-launcher://settings/plugins", "settings/plugins")]
        [TestCase("flow-launcher://settings/store", "settings/store")]
        [TestCase("flow-launcher://settings/theme", "settings/theme")]
        [TestCase("flow-launcher://settings/hotkey", "settings/hotkey")]
        [TestCase("flow-launcher://settings/proxy", "settings/proxy")]
        [TestCase("flow-launcher://settings/about", "settings/about")]
        [TestCase("FLOW-LAUNCHER://Settings/Plugins", "settings/plugins")]
        public void SettingsPages_EveryDocumentedLink_MapsToAPane(string payload, string expectedVerb)
        {
            var ok = DeepLink.TryParse(payload, out var verb, out _);
            Assert.That(ok, Is.True);
            Assert.That(verb, Is.EqualTo(expectedVerb));
            Assert.That(DeepLink.SettingsPages.ContainsKey(verb), Is.True);
        }

        [Test]
        public void SettingsPages_UnknownSubpage_NotMapped()
        {
            var ok = DeepLink.TryParse("flow-launcher://settings/nope", out var verb, out _);
            Assert.That(ok, Is.True);
            Assert.That(verb, Is.EqualTo("settings/nope"));
            Assert.That(DeepLink.SettingsPages.ContainsKey(verb), Is.False);
        }

        [Test]
        public void TryParse_SettingsPluginsWithPluginId_ExtractsParameter()
        {
            var ok = DeepLink.TryParse("flow-launcher://settings/plugins?plugin=abc-123", out var verb, out var parameters);
            Assert.That(ok, Is.True);
            Assert.That(verb, Is.EqualTo("settings/plugins"));
            Assert.That(parameters["plugin"], Is.EqualTo("abc-123"));
        }

        [Test]
        public void TryParse_SettingsStoreWithQuery_ExtractsParameter()
        {
            var ok = DeepLink.TryParse("flow-launcher://settings/store?q=clipboard%20history", out var verb, out var parameters);
            Assert.That(ok, Is.True);
            Assert.That(verb, Is.EqualTo("settings/store"));
            Assert.That(parameters["q"], Is.EqualTo("clipboard history"));
        }
    }
}
