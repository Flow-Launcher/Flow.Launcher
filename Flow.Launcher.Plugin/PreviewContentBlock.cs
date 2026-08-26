using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// A single piece of content shown in the preview panel of a <see cref="Result"/>.
    /// Concrete subclasses carry only the fields relevant to their content type.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(MarkdownPreviewBlock), "markdown")]
    [JsonDerivedType(typeof(SeparatorPreviewBlock), "separator")]
    [JsonDerivedType(typeof(TextPreviewBlock), "text")]
    public abstract record PreviewContentBlock
    {
    }

    /// <summary>
    /// Preview content rendered as formatted markdown.
    /// </summary>
    public sealed record MarkdownPreviewBlock : PreviewContentBlock
    {
        /// <summary>
        /// The inline markdown source to render.
        /// </summary>
        public string InlineMarkdown { get; set; }
    }

    /// <summary>
    /// A horizontal line shown in the preview panel.
    /// </summary>
    public sealed record SeparatorPreviewBlock : PreviewContentBlock
    {
    }

    /// <summary>
    /// Preview content rendered as plain wrapped text.
    /// </summary>
    public sealed record TextPreviewBlock : PreviewContentBlock
    {
        /// <summary>
        /// The plain text to display.
        /// </summary>
        public string Text { get; set; }
    }
}
