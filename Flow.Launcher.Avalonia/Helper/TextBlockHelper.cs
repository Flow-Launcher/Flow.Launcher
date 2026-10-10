using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Attached properties for rendering search-highlighted text on a TextBlock.
/// Set <see cref="HighlightTextProperty"/> and <see cref="HighlightDataProperty"/>; inlines are built
/// directly on the TextBlock (plain <see cref="TextBlock.Text"/> when there is nothing to highlight).
/// </summary>
public static class TextBlockHelper
{
    private const string HighlightBrushKey = "HighlightForegroundBrush";

    public static readonly AttachedProperty<string?> HighlightTextProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, string?>("HighlightText", typeof(TextBlockHelper));

    public static readonly AttachedProperty<IList<int>?> HighlightDataProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, IList<int>?>("HighlightData", typeof(TextBlockHelper));

    static TextBlockHelper()
    {
        HighlightTextProperty.Changed.AddClassHandler<TextBlock>(OnHighlightChanged);
        HighlightDataProperty.Changed.AddClassHandler<TextBlock>(OnHighlightChanged);
    }

    public static string? GetHighlightText(TextBlock textBlock) => textBlock.GetValue(HighlightTextProperty);

    public static void SetHighlightText(TextBlock textBlock, string? value) => textBlock.SetValue(HighlightTextProperty, value);

    public static IList<int>? GetHighlightData(TextBlock textBlock) => textBlock.GetValue(HighlightDataProperty);

    public static void SetHighlightData(TextBlock textBlock, IList<int>? value) => textBlock.SetValue(HighlightDataProperty, value);

    private static void OnHighlightChanged(TextBlock textBlock, AvaloniaPropertyChangedEventArgs e)
    {
        var text = textBlock.GetValue(HighlightTextProperty) ?? string.Empty;
        var highlightData = textBlock.GetValue(HighlightDataProperty);

        if (highlightData is not { Count: > 0 } || text.Length == 0)
        {
            if (textBlock.Inlines is { Count: > 0 } existing)
                existing.Clear();
            textBlock.Text = text;
            return;
        }

        // InlineCollection.Add prepends a Run of any existing Text; clear it first.
        textBlock.Text = null;
        var inlines = textBlock.Inlines ??= new InlineCollection();
        inlines.Clear();

        var marks = new bool[text.Length];
        foreach (var index in highlightData)
        {
            if ((uint)index < (uint)text.Length)
                marks[index] = true;
        }

        var brush = ResolveHighlightBrush();
        var runStart = 0;
        var currentIsHighlight = marks[0];

        for (var i = 1; i < text.Length; i++)
        {
            if (marks[i] == currentIsHighlight)
                continue;

            inlines.Add(CreateRun(text.Substring(runStart, i - runStart), currentIsHighlight, brush));
            runStart = i;
            currentIsHighlight = marks[i];
        }

        inlines.Add(CreateRun(text.Substring(runStart), currentIsHighlight, brush));
    }

    // Resolved once per rebuild (not per run) so theme/resource changes still apply to new rows.
    private static IBrush ResolveHighlightBrush()
    {
        if (Application.Current != null &&
            Application.Current.TryGetResource(HighlightBrushKey, null, out var resource) &&
            resource is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Colors.Gold);
    }

    private static Run CreateRun(string text, bool isHighlight, IBrush brush)
    {
        var run = new Run(text);
        if (isHighlight)
        {
            run.FontWeight = FontWeight.Bold;
            run.Foreground = brush;
        }

        return run;
    }
}
