using System;
using System.Collections.Generic;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test
{
    [TestFixture]
    public class PluginManagerPreviewTest
    {
        [Test]
        public void GivenNullBlockEntry_WhenValidatingResults_ThenThrowsWithTheBlockIndex()
        {
            var results = new List<Result>
            {
                new()
                {
                    Title = "Malformed",
                    PreviewDefinition = new BlocksPreview
                    {
                        ContentBlocks = [new TextPreviewBlock { Text = "Line" }, null]
                    }
                }
            };

            var exception = Assert.Throws<InvalidOperationException>(
                () => PluginManager.ThrowIfBlocksPreviewInvalid(results));

            ClassicAssert.IsTrue(exception.Message.Contains("index 1"), exception.Message);
        }

        [Test]
        public void GivenValidPreviews_WhenValidatingResults_ThenDoesNotThrow()
        {
            var results = new List<Result>
            {
                new()
                {
                    Title = "Valid",
                    PreviewDefinition = new BlocksPreview
                    {
                        ContentBlocks = [new TextPreviewBlock { Text = "Line" }]
                    }
                },
                new()
                {
                    Title = "No preview"
                }
            };

            Assert.DoesNotThrow(() => PluginManager.ThrowIfBlocksPreviewInvalid(results));
        }
    }
}
