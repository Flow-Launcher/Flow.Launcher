using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Attached properties for TextBlock to enable binding Inlines from converters.
/// </summary>
public static class TextBlockHelper
{
    /// <summary>
    /// Attached property for setting formatted text with highlights on a TextBlock.
    /// Bind to this with a MultiBinding + HighlightTextConverter to get highlighted search results.
    /// </summary>
    public static readonly AttachedProperty<InlineCollection?> FormattedTextProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, InlineCollection?>(
            "FormattedText",
            typeof(TextBlockHelper));

    static TextBlockHelper()
    {
        FormattedTextProperty.Changed.AddClassHandler<TextBlock>(OnFormattedTextChanged);
    }

    public static InlineCollection? GetFormattedText(TextBlock textBlock)
        => textBlock.GetValue(FormattedTextProperty);

    public static void SetFormattedText(TextBlock textBlock, InlineCollection? value)
        => textBlock.SetValue(FormattedTextProperty, value);

    private static void OnFormattedTextChanged(TextBlock textBlock, AvaloniaPropertyChangedEventArgs e)
    {
        textBlock.Inlines?.Clear();

        if (e.NewValue is InlineCollection inlines)
        {
            // We need to copy the inlines because they can only belong to one parent
            foreach (var inline in inlines)
            {
                var clone = CloneInline(inline);
                if (clone != null)
                {
                    textBlock.Inlines?.Add(clone);
                }
            }
        }
    }

    private static Inline? CloneInline(Inline inline)
    {
        if (inline is Run run)
        {
            return new Run(run.Text)
            {
                FontWeight = run.FontWeight,
                Foreground = run.Foreground
            };
        }

        if (inline is Span span)
        {
            var clone = new Span();
            foreach (var child in span.Inlines)
            {
                var childClone = CloneInline(child);
                if (childClone != null)
                {
                    clone.Inlines.Add(childClone);
                }
            }

            return clone;
        }

        if (inline is LineBreak)
        {
            return new LineBreak();
        }

        return null;
    }
}
