using System;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using MdXaml;

namespace Flow.Launcher.Resources.Controls;

public class PreviewMarkdownScrollViewer : MarkdownScrollViewer
{
    private static readonly string[] EmphasisMarkers = ["**", "__", "*", "_"];

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == MarkdownProperty)
        {
            _ = Dispatcher.BeginInvoke(ApplyMarkdownCompatibilityFixes, DispatcherPriority.Loaded);
        }
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        _ = Dispatcher.BeginInvoke(ApplyMarkdownCompatibilityFixes, DispatcherPriority.Loaded);
    }

    private void ApplyMarkdownCompatibilityFixes()
    {
        if (Document is null)
        {
            return;
        }

        ApplyMarkdownCompatibilityFixes(Document.Blocks);
    }

    private static void ApplyMarkdownCompatibilityFixes(BlockCollection blocks)
    {
        foreach (var block in blocks.ToList())
        {
            switch (block)
            {
                case Paragraph paragraph:
                    ApplyMarkdownCompatibilityFixes(paragraph.Inlines);
                    break;

                case Section section:
                    ApplyMarkdownCompatibilityFixes(section.Blocks);
                    break;

                case System.Windows.Documents.List list:
                    foreach (var listItem in list.ListItems.ToList())
                    {
                        ApplyMarkdownCompatibilityFixes(listItem.Blocks);
                    }

                    break;

                case Table table:
                    foreach (var cell in table.RowGroups.ToList()
                                 .SelectMany(rowGroup => rowGroup.Rows.ToList())
                                 .SelectMany(row => row.Cells.ToList()))
                    {
                        ApplyMarkdownCompatibilityFixes(cell.Blocks);
                    }

                    break;
            }
        }
    }

    private static void ApplyMarkdownCompatibilityFixes(InlineCollection inlineCollection)
    {
        foreach (var span in inlineCollection.OfType<Span>().Where(span => !IsCodeSpan(span)).ToList())
        {
            ApplyMarkdownCompatibilityFixes(span.Inlines);
        }

        var inlines = inlineCollection.ToList();
        for (var i = 1; i < inlines.Count - 1; i++)
        {
            var inline = inlines[i];
            if (!IsCodeSpan(inline) || inlines[i - 1] is not Run before || inlines[i + 1] is not Run after)
            {
                continue;
            }

            var marker = EmphasisMarkers.FirstOrDefault(value =>
                before.Text.EndsWith(value, StringComparison.Ordinal) &&
                after.Text.StartsWith(value, StringComparison.Ordinal));

            if (marker is null)
            {
                continue;
            }

            before.Text = before.Text[..^marker.Length];
            after.Text = after.Text[marker.Length..];

            if (marker.Length == 2)
            {
                inline.FontWeight = FontWeights.Bold;
            }
            else
            {
                inline.FontStyle = FontStyles.Italic;
            }
        }

        foreach (var run in inlineCollection.OfType<Run>().Where(run => run.Text.Length == 0).ToList())
        {
            inlineCollection.Remove(run);
        }
    }

    private static bool IsCodeSpan(Inline inline)
    {
        return inline.Tag is string tag && tag.Equals("CodeSpan", StringComparison.Ordinal);
    }
}
