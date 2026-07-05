using Flow.Launcher.Resources.Controls;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test
{
    [TestFixture]
    internal class CodeHighlightThemeTest
    {
        private string _savedThemeName;

        [SetUp]
        public void SetUp() => _savedThemeName = PreviewMarkdownScrollViewer.ActiveThemeName;

        [TearDown]
        public void TearDown() => PreviewMarkdownScrollViewer.ApplyCodeHighlightTheme(_savedThemeName, isDark: true);

        [Test]
        public void GivenAutoSetting_WhenAppIsDark_ThenActiveThemeIsADarkTheme()
        {
            PreviewMarkdownScrollViewer.ApplyCodeHighlightTheme("Auto", isDark: true);

            ClassicAssert.AreEqual("VS Code Dark+", PreviewMarkdownScrollViewer.ActiveThemeName);
        }

        [Test]
        public void GivenAutoSetting_WhenAppIsLight_ThenActiveThemeIsALightTheme()
        {
            PreviewMarkdownScrollViewer.ApplyCodeHighlightTheme("Auto", isDark: false);

            ClassicAssert.AreEqual("VS Code Light", PreviewMarkdownScrollViewer.ActiveThemeName);
        }

        [Test]
        public void GivenExplicitTheme_WhenApplied_ThenActiveThemeMatchesRegardlessOfAppScheme()
        {
            PreviewMarkdownScrollViewer.ApplyCodeHighlightTheme("OneDark", isDark: false);

            ClassicAssert.AreEqual("One Dark", PreviewMarkdownScrollViewer.ActiveThemeName);
        }

        [Test]
        public void GivenEmptySetting_WhenApplied_ThenFallsBackToAutoBehaviour()
        {
            PreviewMarkdownScrollViewer.ApplyCodeHighlightTheme("", isDark: false);

            ClassicAssert.AreEqual("VS Code Light", PreviewMarkdownScrollViewer.ActiveThemeName);
        }

        [Test]
        public void GivenUnrecognisedSetting_WhenApplied_ThenFallsBackToAutoBehaviour()
        {
            PreviewMarkdownScrollViewer.ApplyCodeHighlightTheme("BogusTheme", isDark: true);

            ClassicAssert.AreEqual("VS Code Dark+", PreviewMarkdownScrollViewer.ActiveThemeName);
        }
    }
}
