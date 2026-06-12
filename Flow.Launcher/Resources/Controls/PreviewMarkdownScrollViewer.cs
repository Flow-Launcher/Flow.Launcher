using System;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using MdXaml;

namespace Flow.Launcher.Resources.Controls;

public class PreviewMarkdownScrollViewer : MarkdownScrollViewer
{
    private static readonly string[] EmphasisMarkers = ["**", "__", "*", "_"];
    private const double MinimumDocumentPageWidth = 64;

    internal static CodeHighlightTheme ActiveTheme { get; set; } = CodeHighlightTheme.VSCodeDarkPlus;

    static PreviewMarkdownScrollViewer()
    {
        // AvalonEdit ships no AHK highlighter; map AHK language tags to the C++ definition so
        // MdXaml fenced blocks tagged ```ahk / ```autohotkey get C-style coloring instead of plain text.
        var cpp = HighlightingManager.Instance.GetDefinition("C++");
        if (cpp is not null)
        {
            foreach (var alias in new[] { "autohotkey", "AutoHotkey", "ahk", "AHK", "ahk2", "AHK2" })
            {
                if (HighlightingManager.Instance.GetDefinition(alias) is null)
                {
                    HighlightingManager.Instance.RegisterHighlighting(alias, [".ahk"], cpp);
                }
            }
        }
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == MarkdownProperty)
        {
            UpdateDocumentPageWidth(ActualWidth);
            _ = Dispatcher.BeginInvoke(ApplyMarkdownCompatibilityFixes, DispatcherPriority.Loaded);
        }
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        UpdateDocumentPageWidth(ActualWidth);
        _ = Dispatcher.BeginInvoke(ApplyMarkdownCompatibilityFixes, DispatcherPriority.Loaded);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        UpdateDocumentPageWidth(arrangeSize.Width);
        var arranged = base.ArrangeOverride(arrangeSize);
        UpdateDocumentPageWidth(arrangeSize.Width);
        return arranged;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateDocumentPageWidth(sizeInfo.NewSize.Width);
    }

    private void UpdateDocumentPageWidth(double width)
    {
        if (Document is null || double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
        {
            return;
        }

        var pageWidth = Math.Max(MinimumDocumentPageWidth, width);
        if (!AreClose(Document.PageWidth, pageWidth))
        {
            Document.PageWidth = pageWidth;
        }

        if (!AreClose(Document.MaxPageWidth, pageWidth))
        {
            Document.MaxPageWidth = pageWidth;
        }

        if (!AreClose(Document.ColumnWidth, pageWidth))
        {
            Document.ColumnWidth = pageWidth;
        }
    }

    private static bool AreClose(double left, double right)
        => Math.Abs(left - right) < 0.5;

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

                case BlockUIContainer { Child: TextEditor editor }:
                    RetintEditor(editor);
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
                inline.FontWeight = FontWeights.SemiBold;
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

    private static void RetintEditor(TextEditor editor)
    {
        var definition = editor.SyntaxHighlighting;
        if (definition is null)
        {
            return;
        }

        var theme = ActiveTheme;

        var defaultForeground = new SolidColorBrush(theme.DefaultForeground);
        defaultForeground.Freeze();
        editor.Foreground = defaultForeground;

        var changed = false;
        foreach (var color in definition.NamedHighlightingColors)
        {
            if (color.Name is null || !theme.TryGetColor(color.Name, out var target))
            {
                continue;
            }

            var brush = new SolidHighlightingBrush(target);
            if (!Equals(color.Foreground, brush))
            {
                color.Foreground = brush;
                changed = true;
            }
        }

        if (changed)
        {
            editor.TextArea.TextView.Redraw();
        }
    }

    private sealed class SolidHighlightingBrush : HighlightingBrush
    {
        private readonly SolidColorBrush _brush;

        public SolidHighlightingBrush(Color color)
        {
            _brush = new SolidColorBrush(color);
            _brush.Freeze();
        }

        public override Brush GetBrush(ITextRunConstructionContext context) => _brush;

        public override string ToString() => _brush.Color.ToString();
    }
}
