using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Flow.Launcher.Infrastructure.Logger;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using MdXaml;

namespace Flow.Launcher.Resources.Controls;

public class PreviewMarkdownScrollViewer : MarkdownScrollViewer
{
    private static readonly string[] EmphasisMarkers = ["**", "__", "*", "_"];
    private const double MinimumDocumentPageWidth = 64;

    internal static CodeHighlightTheme ActiveTheme { get; private set; } = CodeHighlightTheme.VSCodeDarkPlus;

    // Track in-flight BeginInvoke so rapid Markdown changes don't pile up traversals.
    private DispatcherOperation _pendingFixOperation;

    /// <summary>
    /// Name of the code-highlight theme currently applied to embedded code blocks.
    /// </summary>
    public static string ActiveThemeName => ActiveTheme.Name;

    /// <summary>
    /// Applies the configured code-highlight theme to subsequently rendered code blocks.
    /// </summary>
    /// <param name="setting">The persisted Settings.CodeHighlightTheme value (e.g. "Auto", "OneDark").</param>
    /// <param name="isDark">Whether the app currently renders with a dark colour scheme.</param>
    public static void ApplyCodeHighlightTheme(string setting, bool isDark)
        => ActiveTheme = CodeHighlightTheme.Resolve(setting, isDark);

    /// <summary>
    /// Returns true when <paramref name="source"/> (typically the focused element of a routed key
    /// event) sits inside one of the code blocks embedded in a markdown preview.
    /// </summary>
    public static bool IsCodeBlockFocused(object source)
    {
        var element = source as DependencyObject;
        while (element is not null)
        {
            if (element is TextEditor)
            {
                return true;
            }

            element = element is Visual
                ? VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        }

        return false;
    }

    /// <summary>
    /// Returns true when <paramref name="key"/> is used by the code-block for caret movement
    /// These should be captured by the code block exclusively when its in focus.
    /// </summary>
    /// <remarks>
    /// Modifier variants (Shift+Up, Ctrl+Left) are covered here too since WPF reports the
    /// base key regardless of modifiers.
    /// </remarks>
    public static bool IsCodeBlockNavigationKey(Key key)
    {
        return key is 
            Key.Up or Key.Down or Key.Left or Key.Right
            or Key.PageUp or Key.PageDown
            or Key.Home or Key.End;
    }

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

    public PreviewMarkdownScrollViewer()
    {
        // Bring-into-view is not needed in the preview panel, 
        // and it doesn't work correctly anyway inside nested content blocks, 
        // where it scrolls to the top of the block instead of the focused part, 
        // such as a clicked code block
        AddHandler(RequestBringIntoViewEvent, new RequestBringIntoViewEventHandler((_, e) => e.Handled = true), handledEventsToo: true);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == MarkdownProperty)
        {
            UpdateDocumentPageWidth(ActualWidth);
            ScheduleCompatibilityFixes();
        }
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        CommandBindings.Add(new CommandBinding(NavigationCommands.GoToPage, OpenHyperlink));
        UpdateDocumentPageWidth(ActualWidth);
        ScheduleCompatibilityFixes();
    }

    private static void OpenHyperlink(object sender, ExecutedRoutedEventArgs e)
    {
        var uri = e.Parameter as Uri;
        if (uri is null)
        {
            if (e.Parameter is not string s)
            {
                Log.Warn(nameof(PreviewMarkdownScrollViewer),
                    $"Unexpected hyperlink parameter type: {e.Parameter?.GetType().Name ?? "null"}",
                    nameof(OpenHyperlink));
                return;
            }
            else
            {
                try
                {
                    uri = new Uri(s);
                }
                catch (UriFormatException)
                {
                    Log.Warn(nameof(PreviewMarkdownScrollViewer),
                        $"Unable to parse hyperlink URL: \"{s}\"",
                        nameof(OpenHyperlink));
                    return;
                }
            }
        }

        // Relative URIs have no base to resolve against so can't be used as web addresses.
        if (!uri.IsAbsoluteUri)
        {
            Log.Warn(nameof(PreviewMarkdownScrollViewer),
                $"Skipping relative hyperlink: {uri}",
                nameof(OpenHyperlink));
            return;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            Log.Warn(nameof(PreviewMarkdownScrollViewer),
                $"Skipping hyperlink with unsafe scheme \"{uri.Scheme}\": {uri}",
                nameof(OpenHyperlink));
            return;
        }

        try
        {
            App.API.OpenWebUrl(uri);
        }
        catch (Exception ex)
        {
            Log.Error(nameof(PreviewMarkdownScrollViewer),
                $"Failed to open URL \"{uri}\": {ex.Message}",
                nameof(OpenHyperlink));
            return;
        }

        App.API.HideMainWindow();
        e.Handled = true;
    }

    private void ScheduleCompatibilityFixes()
    {
        // Skip if a traversal is already queued.
        if (_pendingFixOperation is { Status: DispatcherOperationStatus.Pending })
            return;

        _pendingFixOperation = Dispatcher.BeginInvoke(ApplyMarkdownCompatibilityFixes, DispatcherPriority.Loaded);
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

        var transformers = editor.TextArea.TextView.LineTransformers;
        if (transformers.OfType<ThemedHighlightingColorizer>().Any(colorizer => colorizer.Theme == theme))
        {
            return;
        }

        // Colorize through a per-editor transformer instead of mutating the shared highlighting
        // definition, so the tint cannot leak into other consumers of the same definition.
        foreach (var colorizer in transformers.OfType<HighlightingColorizer>().ToList())
        {
            transformers.Remove(colorizer);
        }

        // Index 0 mirrors TextEditor's own colorizer placement: syntax colors must be applied
        // before the selection colorizer so selected text still gets the selection foreground.
        transformers.Insert(0, new ThemedHighlightingColorizer(definition, theme));
    }

    private sealed class ThemedHighlightingColorizer : HighlightingColorizer
    {
        private readonly Dictionary<HighlightingColor, HighlightingColor> _themedColors = new();

        public ThemedHighlightingColorizer(IHighlightingDefinition definition, CodeHighlightTheme theme)
            : base(definition)
        {
            Theme = theme;
        }

        public CodeHighlightTheme Theme { get; }

        // Theme at the highlighter level rather than in ApplyColorToElement: the base colorizer
        // skips styling-free colors (e.g. C#'s attribute-less "Punctuation") before they would
        // ever reach ApplyColorToElement.
        protected override IHighlighter CreateHighlighter(TextView textView, TextDocument document)
            => new ThemedHighlighter(base.CreateHighlighter(textView, document), this);

        private HighlightingColor ThemedColor(HighlightingColor color)
        {
            if (color?.Name is null || !Theme.TryGetColor(color.Name, out var target))
            {
                return color;
            }

            if (!_themedColors.TryGetValue(color, out var themed))
            {
                themed = color.Clone();
                themed.Foreground = new SolidHighlightingBrush(target);
                themed.Freeze();
                _themedColors[color] = themed;
            }

            return themed;
        }

        private sealed class ThemedHighlighter : IHighlighter
        {
            private readonly IHighlighter _inner;
            private readonly ThemedHighlightingColorizer _colorizer;

            public ThemedHighlighter(IHighlighter inner, ThemedHighlightingColorizer colorizer)
            {
                _inner = inner;
                _colorizer = colorizer;
            }

            public IDocument Document => _inner.Document;

            public HighlightingColor DefaultTextColor => _colorizer.ThemedColor(_inner.DefaultTextColor);

            public event HighlightingStateChangedEventHandler HighlightingStateChanged
            {
                add => _inner.HighlightingStateChanged += value;
                remove => _inner.HighlightingStateChanged -= value;
            }

            public HighlightedLine HighlightLine(int lineNumber)
            {
                var line = _inner.HighlightLine(lineNumber);
                foreach (var section in line.Sections)
                {
                    section.Color = _colorizer.ThemedColor(section.Color);
                }

                return line;
            }

            public IEnumerable<HighlightingColor> GetColorStack(int lineNumber) => _inner.GetColorStack(lineNumber);

            public HighlightingColor GetNamedColor(string name) => _colorizer.ThemedColor(_inner.GetNamedColor(name));

            public void UpdateHighlightingState(int lineNumber) => _inner.UpdateHighlightingState(lineNumber);

            public void BeginHighlighting() => _inner.BeginHighlighting();

            public void EndHighlighting() => _inner.EndHighlighting();

            public void Dispose() => _inner.Dispose();
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

        public override bool Equals(object obj)
            => obj is SolidHighlightingBrush other && _brush.Color == other._brush.Color;

        public override int GetHashCode() => _brush.Color.GetHashCode();
    }
}
