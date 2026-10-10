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
    private const string HighlightFontWeightKey = "HighlightFontWeight";

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

        var (brush, weight) = ResolveHighlightStyle();
        var runStart = 0;
        var currentIsHighlight = marks[0];

        for (var i = 1; i < text.Length; i++)
        {
            if (marks[i] == currentIsHighlight)
                continue;

            inlines.Add(CreateRun(text.Substring(runStart, i - runStart), currentIsHighlight, brush, weight));
            runStart = i;
            currentIsHighlight = marks[i];
        }

        inlines.Add(CreateRun(text.Substring(runStart), currentIsHighlight, brush, weight));
    }

    // Resolved once per rebuild (not per run) so theme/resource changes still apply to new rows. Rows are often not yet
    // attached when this runs, so the lookup goes to the application. Themes supply both values through
    // Helper/ThemeLoader.cs; Themes/Resources.axaml has the gold default and bold is the fallback weight.
    private static (IBrush Brush, FontWeight Weight) ResolveHighlightStyle()
    {
        var application = Application.Current;
        var variant = application?.ActualThemeVariant;
        var brush = application != null && application.TryGetResource(HighlightBrushKey, variant, out var brushResource) && brushResource is IBrush themeBrush
            ? themeBrush
            : Brushes.Gold;
        var weight = application != null && application.TryGetResource(HighlightFontWeightKey, variant, out var weightResource) && weightResource is FontWeight themeWeight
            ? themeWeight
            : FontWeight.Bold;
        return (brush, weight);
    }

    private static Run CreateRun(string text, bool isHighlight, IBrush brush, FontWeight weight)
    {
        var run = new Run(text);
        if (isHighlight)
        {
            run.FontWeight = weight;
            run.Foreground = brush;
        }

        return run;
    }
}
