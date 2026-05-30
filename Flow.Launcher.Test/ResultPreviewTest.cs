using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;
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

        [Test]
        public void GivenResultWithIconDelegate_WhenMetadataUpdated_ThenPluginIconIsNotInjected()
        {
            var result = new Result
            {
                Title = "Delegate icon",
                Icon = () => null
            };

            PluginManager.UpdatePluginMetadata([result], MetadataWithIcon(), new Query());

            ClassicAssert.IsNull(result.IcoPath);
            ClassicAssert.IsNotNull(result.Icon);
        }

        [Test]
        public void GivenResultWithGlyphIcon_WhenMetadataUpdated_ThenPluginIconIsNotInjected()
        {
            var result = new Result
            {
                Title = "Glyph icon",
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\uE8A5")
            };

            PluginManager.UpdatePluginMetadata([result], MetadataWithIcon(), new Query());

            ClassicAssert.IsNull(result.IcoPath);
            ClassicAssert.IsNotNull(result.Glyph);
        }

        [Test]
        public void GivenResultWithoutIconSource_WhenMetadataUpdated_ThenPluginIconIsInjected()
        {
            var result = new Result
            {
                Title = "Metadata fallback"
            };

            PluginManager.UpdatePluginMetadata([result], MetadataWithIcon(), new Query());

            ClassicAssert.AreEqual("Images\\plugin.png", result.IcoPath);
        }

        [Test]
        public void GivenHiddenPreviewResult_WhenCheckedForSuppression_ThenPreviewIsSuppressed()
        {
            var result = new Result
            {
                Title = "Hidden preview",
                Preview = new Result.PreviewInfo
                {
                    ContentType = PreviewContentType.Hidden
                }
            };

            var viewModel = new ResultViewModel(result, new Settings());

            ClassicAssert.IsTrue(MainViewModel.ShouldSuppressPreview(viewModel));
        }

        [Test]
        public void GivenMarkdownPreviewResult_WhenCheckedForSuppression_ThenPreviewIsNotSuppressed()
        {
            var result = new Result
            {
                Title = "Markdown preview",
                Preview = new Result.PreviewInfo
                {
                    ContentType = PreviewContentType.Markdown
                }
            };

            var viewModel = new ResultViewModel(result, new Settings());

            ClassicAssert.IsFalse(MainViewModel.ShouldSuppressPreview(viewModel));
        }

        [Test]
        public void GivenPreviewAssignedAfterPluginDirectory_WhenRelativePreviewImagePathSet_ThenRelativePathIsPreservedAndAbsolutePathIsResolved()
        {
            var pluginDirectory = Path.Combine("C:\\Plugins", "PreviewTest");
            var result = new Result
            {
                Title = "Preview image",
                PluginDirectory = pluginDirectory
            };

            result.Preview = new Result.PreviewInfo
            {
                PreviewImagePath = "Images\\preview.png"
            };

            ClassicAssert.AreEqual("Images\\preview.png", result.Preview.PreviewImagePath);
            ClassicAssert.AreEqual(Path.Combine(pluginDirectory, "Images\\preview.png"), result.Preview.PreviewImagePathAbsolute);
        }

        [Test]
        public void GivenPreviewImagePathChangedAfterPluginDirectory_WhenRelativePreviewImagePathSet_ThenRelativePathIsPreservedAndAbsolutePathIsResolved()
        {
            var pluginDirectory = Path.Combine("C:\\Plugins", "PreviewTest");
            var preview = new Result.PreviewInfo();
            var result = new Result
            {
                Title = "Preview image",
                Preview = preview,
                PluginDirectory = pluginDirectory
            };

            preview.PreviewImagePath = "Images\\preview.png";

            ClassicAssert.AreEqual("Images\\preview.png", result.Preview.PreviewImagePath);
            ClassicAssert.AreEqual(Path.Combine(pluginDirectory, "Images\\preview.png"), result.Preview.PreviewImagePathAbsolute);
        }

        [Test]
        public void GivenDefaultPreviewUsedByPreviousResult_WhenNewDefaultPreviewGetsRelativePath_ThenPreviousPluginDirectoryDoesNotLeak()
        {
            var previousPluginDirectory = Path.Combine("C:\\Plugins", "Previous");
            _ = new Result
            {
                Title = "Previous preview",
                Preview = Result.PreviewInfo.Default,
                PluginDirectory = previousPluginDirectory
            };

            var nextDefault = Result.PreviewInfo.Default;
            nextDefault.PreviewImagePath = "Images\\preview.png";

            ClassicAssert.AreEqual("Images\\preview.png", nextDefault.PreviewImagePath);
            ClassicAssert.AreEqual("Images\\preview.png", nextDefault.PreviewImagePathAbsolute);
        }

        [Test]
        public void GivenRelativePreviewImagePathAfterPluginDirectory_WhenSerialized_ThenSerializedPathStaysRelative()
        {
            var result = new Result
            {
                Title = "Preview image",
                PluginDirectory = Path.Combine("C:\\Plugins", "PreviewTest"),
                Preview = new Result.PreviewInfo
                {
                    PreviewImagePath = "Images\\preview.png"
                }
            };

            var json = JsonSerializer.Serialize(result.Preview);

            StringAssert.Contains("\"PreviewImagePath\":\"Images\\\\preview.png\"", json);
            StringAssert.DoesNotContain("C:\\\\Plugins", json);
        }

        [Test]
        public void GivenResultCloneWithPreviewImage_WhenClonePluginDirectoryChanges_ThenOriginalPreviewPathIsUnchanged()
        {
            var originalPluginDirectory = Path.Combine("C:\\Plugins", "Original");
            var clonePluginDirectory = Path.Combine("C:\\Plugins", "Clone");
            var result = new Result
            {
                Title = "Preview image",
                PluginDirectory = originalPluginDirectory,
                Preview = new Result.PreviewInfo
                {
                    PreviewImagePath = "Images\\preview.png"
                }
            };

            var clone = result.Clone();
            clone.PluginDirectory = clonePluginDirectory;

            ClassicAssert.AreEqual(
                Path.Combine(originalPluginDirectory, "Images\\preview.png"),
                result.Preview.PreviewImagePathAbsolute);
            ClassicAssert.AreEqual(
                Path.Combine(clonePluginDirectory, "Images\\preview.png"),
                clone.Preview.PreviewImagePathAbsolute);
            ClassicAssert.AreNotSame(result.Preview, clone.Preview);
        }

        [Test]
        public void GivenResultCloneWithBadgeIcon_WhenClonePluginDirectoryChanges_ThenBadgeIconIsRebased()
        {
            var originalPluginDirectory = Path.Combine("C:\\Plugins", "Original");
            var clonePluginDirectory = Path.Combine("C:\\Plugins", "Clone");
            var result = new Result
            {
                Title = "Badge image",
                PluginDirectory = originalPluginDirectory,
                BadgeIcoPath = "Images\\badge.png"
            };

            var clone = result.Clone();
            clone.PluginDirectory = clonePluginDirectory;

            ClassicAssert.AreEqual(
                Path.Combine(originalPluginDirectory, "Images\\badge.png"),
                result.BadgeIcoPath);
            ClassicAssert.AreEqual(
                Path.Combine(clonePluginDirectory, "Images\\badge.png"),
                clone.BadgeIcoPath);
        }

        [Test]
        public void GivenDialogJumpResultFromPreviewImage_WhenDialogJumpPluginDirectoryChanges_ThenOriginalPreviewPathIsUnchanged()
        {
            var originalPluginDirectory = Path.Combine("C:\\Plugins", "Original");
            var dialogJumpPluginDirectory = Path.Combine("C:\\Plugins", "DialogJump");
            var result = new Result
            {
                Title = "Preview image",
                PluginDirectory = originalPluginDirectory,
                Preview = new Result.PreviewInfo
                {
                    PreviewImagePath = "Images\\preview.png"
                }
            };

            var dialogJumpResult = DialogJumpResult.From(result, "C:\\target");
            dialogJumpResult.PluginDirectory = dialogJumpPluginDirectory;

            ClassicAssert.AreEqual(
                Path.Combine(originalPluginDirectory, "Images\\preview.png"),
                result.Preview.PreviewImagePathAbsolute);
            ClassicAssert.AreEqual(
                Path.Combine(dialogJumpPluginDirectory, "Images\\preview.png"),
                dialogJumpResult.Preview.PreviewImagePathAbsolute);
            ClassicAssert.AreNotSame(result.Preview, dialogJumpResult.Preview);
        }

        [Test]
        public void GivenDialogJumpResultWithBadgeIcon_WhenDialogJumpPluginDirectoryChanges_ThenBadgeIconIsRebased()
        {
            var originalPluginDirectory = Path.Combine("C:\\Plugins", "Original");
            var dialogJumpPluginDirectory = Path.Combine("C:\\Plugins", "DialogJump");
            var result = new Result
            {
                Title = "Badge image",
                PluginDirectory = originalPluginDirectory,
                BadgeIcoPath = "Images\\badge.png"
            };

            var dialogJumpResult = DialogJumpResult.From(result, "C:\\target");
            dialogJumpResult.PluginDirectory = dialogJumpPluginDirectory;

            ClassicAssert.AreEqual(
                Path.Combine(originalPluginDirectory, "Images\\badge.png"),
                result.BadgeIcoPath);
            ClassicAssert.AreEqual(
                Path.Combine(dialogJumpPluginDirectory, "Images\\badge.png"),
                dialogJumpResult.BadgeIcoPath);
        }

        [Test]
        public void GivenResultWithQuerySuggestion_WhenConvertedToDialogJumpResult_ThenQuerySuggestionIsPreserved()
        {
            var result = new Result
            {
                Title = "Dialog jump",
                QuerySuggestionText = "jump to downloads"
            };

            var dialogJumpResult = DialogJumpResult.From(result, "C:\\target");

            ClassicAssert.AreEqual(result.QuerySuggestionText, dialogJumpResult.QuerySuggestionText);
        }

        [Test]
        public void GivenDialogJumpResultWithQuerySuggestion_WhenCloned_ThenQuerySuggestionIsPreserved()
        {
            var result = new DialogJumpResult
            {
                Title = "Dialog jump",
                DialogJumpPath = "C:\\target",
                QuerySuggestionText = "jump to desktop"
            };

            var clone = result.Clone();

            ClassicAssert.AreEqual(result.QuerySuggestionText, clone.QuerySuggestionText);
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public void GivenHiddenPreviewWithExplicitIconAndAnimationAssets_WhenGlyphIconsEnabled_ThenExplicitIconIsPreserved()
        {
            var pluginDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, Path.GetRandomFileName());
            var imagesDirectory = Path.Combine(pluginDirectory, "Images");
            Directory.CreateDirectory(imagesDirectory);
            File.WriteAllText(Path.Combine(imagesDirectory, "Shorty_Flow_Round.png"), string.Empty);
            File.WriteAllText(Path.Combine(imagesDirectory, "Shorty_Ani1.ico"), string.Empty);
            File.WriteAllText(Path.Combine(imagesDirectory, "Shorty_Ani1.png"), string.Empty);
            File.WriteAllText(Path.Combine(imagesDirectory, "Shorty_Ani2.ico"), string.Empty);
            File.WriteAllText(Path.Combine(imagesDirectory, "Shorty_Ani2.png"), string.Empty);
            File.WriteAllText(Path.Combine(imagesDirectory, "Shorty_Eye.ico"), string.Empty);
            File.WriteAllText(Path.Combine(imagesDirectory, "Shorty_Eye.png"), string.Empty);

            try
            {
                var result = new Result
                {
                    Title = "Loading",
                    Preview = new Result.PreviewInfo
                    {
                        ContentType = PreviewContentType.Hidden
                    },
                    IcoPath = "Images\\Shorty_Flow_Round.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\uE8BD"),
                    PluginDirectory = pluginDirectory
                };

                var viewModel = new ResultViewModel(result, new Settings
                {
                    UseGlyphIcons = true
                });

                ClassicAssert.AreEqual("Images\\Shorty_Flow_Round.png", result.IcoPath);
                StringAssert.EndsWith("Images\\Shorty_Flow_Round.png", result.IcoPathAbsolute);
                ClassicAssert.IsNotNull(result.IcoPathAbsolute);
                ClassicAssert.AreEqual(Visibility.Hidden, viewModel.ShowIcon);
                ClassicAssert.AreEqual(Visibility.Visible, viewModel.ShowGlyph);
            }
            finally
            {
                Directory.Delete(pluginDirectory, recursive: true);
            }
        }

        private static PluginMetadata MetadataWithIcon()
        {
            return new PluginMetadata
            {
                ID = "plugin",
                IcoPath = "Images\\plugin.png",
                ActionKeywords = []
            };
        }
    }
}
