using System.Text.Json;
using Flow.Launcher.Plugin;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test
{
    [TestFixture]
    internal class ResultPreviewTest
    {
        [Test]
        public void GivenPreviewWithoutContentType_WhenCreated_ThenDefaultsToText()
        {
            var preview = new Result.PreviewInfo();

            ClassicAssert.AreEqual(PreviewContentType.Text, preview.ContentType);
        }

        [Test]
        public void GivenMarkdownPreview_WhenSerialized_ThenContentTypeIsLowercaseString()
        {
            var preview = new Result.PreviewInfo
            {
                Description = "**markdown**",
                ContentType = PreviewContentType.Markdown
            };

            var json = JsonSerializer.Serialize(preview);

            StringAssert.Contains("\"ContentType\":\"markdown\"", json);
        }
    }
}
