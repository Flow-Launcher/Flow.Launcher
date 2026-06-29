using Flow.Launcher.Resources.Controls;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test
{
    [TestFixture]
    internal class CodeHighlightThemeTest
    {
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
        public void GivenUnknownOrEmptySetting_WhenApplied_ThenFallsBackToAutoBehaviour()
        {
            PreviewMarkdownScrollViewer.ApplyCodeHighlightTheme("", isDark: false);

            ClassicAssert.AreEqual("VS Code Light", PreviewMarkdownScrollViewer.ActiveThemeName);
        }
    }
}
