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
                if (inline is Run run)
                {
                    var newRun = new Run(run.Text)
                    {
                        FontWeight = run.FontWeight
                    };
                    textBlock.Inlines?.Add(newRun);
                }
                else
                {
                    // For other inline types, add directly (may need enhancement)
                    textBlock.Inlines?.Add(inline);
                }
            }
        }
    }
}
