using Flow.Launcher.Core.Plugin;
using NUnit.Framework;

namespace Flow.Launcher.Test
{
    [TestFixture]
    public class PluginInstallerTest
    {
        [TestCase("https://example.com/plugin.zip", "plugin")]
        [TestCase("https://example.com/My.Plugin.zip", "My.Plugin")]
        [TestCase("https://github.com/owner/repo/releases/download/v1/Cool.Plugin.zip", "Cool.Plugin")]
        public void DerivePluginNameFromUrl_ZipUrl_DropsPathAndExtension(string url, string expected)
        {
            Assert.That(PluginInstaller.DerivePluginNameFromUrl(url), Is.EqualTo(expected));
        }

        [Test]
        public void DerivePluginNameFromUrl_UrlWithQueryString_ExcludesQuery()
        {
            Assert.That(PluginInstaller.DerivePluginNameFromUrl("https://example.com/plugin.zip?token=secret"),
                Is.EqualTo("plugin"));
        }

        [Test]
        public void DerivePluginNameFromUrl_EncodedInvalidChar_StripsInvalidChars()
        {
            // %3F decodes to '?', which is invalid in a Windows filename and must be stripped.
            Assert.That(PluginInstaller.DerivePluginNameFromUrl("https://example.com/pl%3Fugin.zip"),
                Is.EqualTo("plugin"));
        }

        [Test]
        public void DerivePluginNameFromUrl_UppercaseExtension_DropsExtensionCaseInsensitively()
        {
            Assert.That(PluginInstaller.DerivePluginNameFromUrl("https://example.com/Plugin.ZIP"),
                Is.EqualTo("Plugin"));
        }

        [Test]
        public void DerivePluginNameFromUrl_NonZipFile_KeepsFullName()
        {
            Assert.That(PluginInstaller.DerivePluginNameFromUrl("https://example.com/readme.txt"),
                Is.EqualTo("readme.txt"));
        }

        [TestCase("https://example.com/%3F.zip")]         // decodes to "?.zip", strips to ".zip", name would be empty
        [TestCase("https://example.com/%3C%3E%7C.zip")]   // "<>|.zip" collapses entirely
        [TestCase("https://example.com/")]                // no basename at all
        public void DerivePluginNameFromUrl_NameCollapsesToEmpty_FallsBackToDefault(string url)
        {
            Assert.That(PluginInstaller.DerivePluginNameFromUrl(url), Is.EqualTo("plugin"));
        }

        [Test]
        public void DerivePluginNameFromUrl_UnparsableUrl_FallsBackToLastSegment()
        {
            // Not an absolute URI, so the naive '/' split fallback applies.
            Assert.That(PluginInstaller.DerivePluginNameFromUrl("relative/path/plugin.zip"),
                Is.EqualTo("plugin"));
        }
    }
}
