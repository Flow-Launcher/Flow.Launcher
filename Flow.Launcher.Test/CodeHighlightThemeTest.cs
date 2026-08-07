using System.Reflection;
using Flow.Launcher.Resources.Controls;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test
{
    [TestFixture]
    internal class CodeHighlightThemeTest
    {
        private static readonly PropertyInfo ActiveThemeProperty =
            typeof(PreviewMarkdownScrollViewer).GetProperty("ActiveTheme",
                BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly FieldInfo ActiveThemeBackingField =
            typeof(PreviewMarkdownScrollViewer).GetField("<ActiveTheme>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);

        private object _savedTheme;

        [SetUp]
        public void SetUp() => _savedTheme = ActiveThemeProperty.GetValue(null);

        [TearDown]
        public void TearDown() => ActiveThemeBackingField.SetValue(null, _savedTheme);

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
