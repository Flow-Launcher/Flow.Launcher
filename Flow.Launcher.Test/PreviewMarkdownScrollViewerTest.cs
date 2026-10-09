using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Flow.Launcher.Resources.Controls;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test
{
    [TestFixture]
    internal class PreviewMarkdownScrollViewerTest
    {
        [Test]
        [Apartment(ApartmentState.STA)]
        public void GivenMarkdownLink_WhenRendered_ThenHyperlinkUsesAccentForeground()
        {
            var accentBrush = new SolidColorBrush(Color.FromRgb(0x31, 0xA8, 0xFF));
            var viewer = new PreviewMarkdownScrollViewer
            {
                MarkdownStyle = CreateMarkdownStyle(accentBrush),
                Markdown = "Links should use the theme accent: [Flow Launcher](https://www.flowlauncher.com/)."
            };

            var hyperlink = EnumerateInlines(viewer.Document.Blocks).OfType<Hyperlink>().Single();
            var foreground = (SolidColorBrush)hyperlink.Foreground;

            ClassicAssert.AreEqual(accentBrush.Color, foreground.Color);
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public void GivenConstrainedPreviewWidth_WhenMarkdownIsRendered_ThenDocumentPageWidthMatchesViewerWidth()
        {
            var viewer = new PreviewMarkdownScrollViewer
            {
                Width = 280,
                Height = 200,
                Markdown = "This long paragraph should wrap inside the preview pane instead of clipping at the right edge."
            };

            viewer.Measure(new Size(280, 200));
            viewer.Arrange(new Rect(0, 0, 280, 200));
            viewer.UpdateLayout();

            ClassicAssert.AreEqual(280, viewer.Document.PageWidth);
            ClassicAssert.AreEqual(280, viewer.Document.MaxPageWidth);
            ClassicAssert.AreEqual(280, viewer.Document.ColumnWidth);
        }

        private static Style CreateMarkdownStyle(Brush accentBrush)
        {
            var documentStyle = new Style(typeof(FlowDocument));
            documentStyle.Setters.Add(new Setter(TextElement.ForegroundProperty, Brushes.LightGray));

            var hyperlinkStyle = new Style(typeof(Hyperlink));
            hyperlinkStyle.Setters.Add(new Setter(TextElement.ForegroundProperty, accentBrush));
            hyperlinkStyle.Setters.Add(new Setter(Inline.TextDecorationsProperty, null));
            documentStyle.Resources.Add(typeof(Hyperlink), hyperlinkStyle);

            return documentStyle;
        }

        private static IEnumerable<Inline> EnumerateInlines(BlockCollection blocks)
        {
            foreach (var block in blocks)
            {
                switch (block)
                {
                    case Paragraph paragraph:
                        foreach (var inline in EnumerateInlines(paragraph.Inlines))
                        {
                            yield return inline;
                        }

                        break;

                    case Section section:
                        foreach (var inline in EnumerateInlines(section.Blocks))
                        {
                            yield return inline;
                        }

                        break;

                    case List list:
                        foreach (var inline in list.ListItems
                                     .Cast<ListItem>()
                                     .SelectMany(item => EnumerateInlines(item.Blocks)))
                        {
                            yield return inline;
                        }

                        break;
                }
            }
        }

        private static IEnumerable<Inline> EnumerateInlines(InlineCollection inlines)
        {
            foreach (var inline in inlines)
            {
                yield return inline;
                if (inline is Span span)
                {
                    foreach (var child in EnumerateInlines(span.Inlines))
                    {
                        yield return child;
                    }
                }
            }
        }
    }
}
