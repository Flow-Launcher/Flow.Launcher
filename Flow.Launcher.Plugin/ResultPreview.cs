using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Describes how Flow renders the preview of a <see cref="Result"/>.
    /// Assign one of the derived types to <see cref="Result.PreviewDefinition"/>.
    /// </summary>
    /// <remarks>
    /// Each derived type is registered here with a <see cref="JsonDerivedTypeAttribute"/> so Flow can
    /// deserialize it from a plugin response. Derived types also override <c>ClonePreview</c> so a
    /// cloned result keeps its own copy of the preview.
    /// </remarks>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(BlocksPreview), "blocks")]
    public abstract record ResultPreview
    {
        private protected ResultPreview() { }

        /// <summary>
        /// Creates a copy that owns its own content, so the caller can change it without affecting the original.
        /// </summary>
        internal abstract ResultPreview ClonePreview();
    }

    /// <summary>
    /// A preview made of an ordered list of content blocks, such as markdown or text.
    /// </summary>
    public sealed record BlocksPreview : ResultPreview
    {
        /// <summary>
        /// Ordered content rendered in the preview panel.
        /// </summary>
        public IList<PreviewContentBlock> ContentBlocks { get; set; } = [];

        /// <summary>
        /// Finds the first block Flow cannot render.
        /// </summary>
        /// <param name="invalidIndex">Index of that block, or -1 when every block can be rendered.</param>
        /// <returns>Whether a block that cannot be rendered was found.</returns>
        internal bool TryGetInvalidBlockIndex(out int invalidIndex)
        {
            if (ContentBlocks is { } blocks)
            {
                for (var index = 0; index < blocks.Count; index++)
                {
                    if (blocks[index] is null)
                    {
                        invalidIndex = index;
                        return true;
                    }
                }
            }

            invalidIndex = -1;
            return false;
        }

        /// <inheritdoc />
        internal override ResultPreview ClonePreview() => this with
        {
            ContentBlocks = ContentBlocks?.Select(block => block with { }).ToList()
        };
    }
}
