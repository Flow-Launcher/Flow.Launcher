using System;

namespace Flow.Launcher.Core.Plugin
{
    /// <summary>
    /// Thrown when a plugin returns a preview block that Flow cannot render.
    /// </summary>
    public sealed class InvalidPreviewBlockException : Exception
    {
        public InvalidPreviewBlockException(int blockIndex, string message) : base(message)
        {
            BlockIndex = blockIndex;
        }

        /// <summary>
        /// Index of that block in the preview content of the result.
        /// </summary>
        public int BlockIndex { get; }
    }
}
