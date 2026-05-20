using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

    private static readonly Dictionary<string, Color> HighlightPalette = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Comment"] = Color.FromRgb(0x6E, 0x73, 0x8D),
        ["Comments"] = Color.FromRgb(0x6E, 0x73, 0x8D),
        ["LineComment"] = Color.FromRgb(0x6E, 0x73, 0x8D),
        ["BlockComment"] = Color.FromRgb(0x6E, 0x73, 0x8D),
        ["DocComment"] = Color.FromRgb(0x6E, 0x73, 0x8D),
        ["String"] = Color.FromRgb(0xA6, 0xDA, 0x95),
        ["Strings"] = Color.FromRgb(0xA6, 0xDA, 0x95),
        ["StringDQ"] = Color.FromRgb(0xA6, 0xDA, 0x95),
        ["StringSQ"] = Color.FromRgb(0xA6, 0xDA, 0x95),
        ["RawString"] = Color.FromRgb(0xA6, 0xDA, 0x95),
        ["Char"] = Color.FromRgb(0xA6, 0xDA, 0x95),
        ["Character"] = Color.FromRgb(0xA6, 0xDA, 0x95),
        ["Keyword"] = Color.FromRgb(0xC6, 0xA0, 0xF6),
        ["Keywords"] = Color.FromRgb(0xC6, 0xA0, 0xF6),
        ["ControlKeyword"] = Color.FromRgb(0xC6, 0xA0, 0xF6),
        ["GotoKeywords"] = Color.FromRgb(0xC6, 0xA0, 0xF6),
        ["ExceptionKeywords"] = Color.FromRgb(0xC6, 0xA0, 0xF6),
        ["MethodKeywords"] = Color.FromRgb(0xC6, 0xA0, 0xF6),
        ["OperatorKeywords"] = Color.FromRgb(0xC6, 0xA0, 0xF6),
        ["NullValue"] = Color.FromRgb(0xC6, 0xA0, 0xF6),
        ["Boolean"] = Color.FromRgb(0xF5, 0xA9, 0x7F),
        ["BooleanConstants"] = Color.FromRgb(0xF5, 0xA9, 0x7F),
        ["Number"] = Color.FromRgb(0xF5, 0xA9, 0x7F),
        ["Numbers"] = Color.FromRgb(0xF5, 0xA9, 0x7F),
        ["NumberLiteral"] = Color.FromRgb(0xF5, 0xA9, 0x7F),
        ["Digits"] = Color.FromRgb(0xF5, 0xA9, 0x7F),
        ["Type"] = Color.FromRgb(0xEE, 0xD4, 0x9F),
        ["Types"] = Color.FromRgb(0xEE, 0xD4, 0x9F),
        ["TypeName"] = Color.FromRgb(0xEE, 0xD4, 0x9F),
        ["Class"] = Color.FromRgb(0xEE, 0xD4, 0x9F),
        ["ClassName"] = Color.FromRgb(0xEE, 0xD4, 0x9F),
        ["ReferenceTypes"] = Color.FromRgb(0xEE, 0xD4, 0x9F),
        ["ValueTypes"] = Color.FromRgb(0xEE, 0xD4, 0x9F),
        ["MethodCall"] = Color.FromRgb(0x8A, 0xAD, 0xF4),
        ["Method"] = Color.FromRgb(0x8A, 0xAD, 0xF4),
        ["MethodName"] = Color.FromRgb(0x8A, 0xAD, 0xF4),
        ["Function"] = Color.FromRgb(0x8A, 0xAD, 0xF4),
        ["Functions"] = Color.FromRgb(0x8A, 0xAD, 0xF4),
        ["Builtin"] = Color.FromRgb(0x91, 0xD7, 0xE3),
        ["Builtins"] = Color.FromRgb(0x91, 0xD7, 0xE3),
        ["Decorator"] = Color.FromRgb(0xC6, 0xA0, 0xF6),
        ["Preprocessor"] = Color.FromRgb(0xC6, 0xA0, 0xF6),
        ["Punctuation"] = Color.FromRgb(0xB8, 0xC0, 0xE0),
        ["Operator"] = Color.FromRgb(0xB8, 0xC0, 0xE0),
        ["Operators"] = Color.FromRgb(0xB8, 0xC0, 0xE0),
        ["BraceMismatch"] = Color.FromRgb(0xED, 0x87, 0x96),
    };

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

                case BlockUIContainer container when container.Child is TextEditor editor:
                    RetintEditor(editor);
                    WrapWithRoundedFrame(container, editor);
                    break;

                case BlockUIContainer { Child: Border { Child: TextEditor wrappedEditor } }:
                    RetintEditor(wrappedEditor);
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

    private static void RetintEditor(TextEditor editor)
    {
        var definition = editor.SyntaxHighlighting;
        if (definition is null)
        {
            return;
        }

        var changed = false;
        foreach (var color in definition.NamedHighlightingColors)
        {
            if (color.Name is null || !HighlightPalette.TryGetValue(color.Name, out var target))
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

    private static void WrapWithRoundedFrame(BlockUIContainer container, TextEditor editor)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Margin = editor.Margin,
            SnapsToDevicePixels = true,
        };
        border.SetResourceReference(Border.BackgroundProperty, "Color03B");
        border.SetResourceReference(Border.BorderBrushProperty, "SeparatorForeground");

        editor.Margin = new Thickness(0);
        editor.BorderThickness = new Thickness(0);
        editor.Background = Brushes.Transparent;

        container.Child = border;
        border.Child = editor;
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
