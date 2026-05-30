using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test
{
    [TestFixture]
    internal class PreviewMarkdownStyleTest
    {
        private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

        [Test]
        public void GivenPreviewMarkdownStyle_WhenRendered_ThenBodyAndBoldUseAppTextFont()
        {
            var style = GetPreviewMarkdownStyle();

            ClassicAssert.AreEqual("{DynamicResource ContentControlThemeFontFamily}", GetSetterValue(style, "FontFamily"));
            ClassicAssert.AreEqual("Normal", GetSetterValue(style, "FontWeight"));

            var boldStyle = style.Descendants(PresentationNamespace + "Style")
                .First(element => element.Attribute("TargetType")?.Value == "{x:Type Bold}");

            ClassicAssert.AreEqual("{DynamicResource ContentControlThemeFontFamily}", GetSetterValue(boldStyle, "FontFamily"));
            ClassicAssert.AreEqual("SemiBold", GetSetterValue(boldStyle, "FontWeight"));
        }

        private static XElement GetPreviewMarkdownStyle()
        {
            var baseThemePath = FindBaseThemePath();
            var document = XDocument.Load(baseThemePath);

            return document.Descendants(PresentationNamespace + "Style")
                .First(element => element.Attribute(XamlNamespace + "Key")?.Value == "PreviewMarkdownStyle");
        }

        private static string FindBaseThemePath()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory is not null)
            {
                var path = Path.Combine(directory.FullName, "Flow.Launcher", "Themes", "Base.xaml");
                if (File.Exists(path))
                {
                    return path;
                }

                directory = directory.Parent;
            }

            throw new FileNotFoundException("Could not find Flow.Launcher/Themes/Base.xaml from test directory.");
        }

        private static string GetSetterValue(XElement style, string propertyName)
        {
            return style.Elements(PresentationNamespace + "Setter")
                .Where(element => element.Attribute("Property")?.Value == propertyName)
                .Select(element => element.Attribute("Value")?.Value)
                .FirstOrDefault();
        }
    }
}
