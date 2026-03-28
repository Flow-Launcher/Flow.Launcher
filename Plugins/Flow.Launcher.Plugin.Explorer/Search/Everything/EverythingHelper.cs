using System.Collections.Generic;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    public static class EverythingHelper
    {
        public enum StateCode
        {
            OK,
            MemoryError,
            IPCError,
            RegisterClassExError,
            CreateWindowError,
            CreateThreadError,
            InvalidIndexError,
            InvalidCallError
        }

        /// <summary>
        /// Convert the highlighted string from Everything API to a list of highlight indexes for our Result.
        /// </summary>
        /// <param name="highlightString">Text inside a * quote is highlighted, two consecutive *'s is a single literal *. For example, in the highlighted text: abc*123* the 123 part is highlighted.</param>
        /// <returns>A list of zero-based character indices that should be highlighted.</returns>
        public static List<int> EverythingHighlightStringToHighlightList(string highlightString)
        {
            var highlightData = new List<int>();

            if (string.IsNullOrEmpty(highlightString))
                return highlightData;

            var isHighlighted = false;
            var actualIndex = 0; // Index in the actual string (without * markers)
            var length = highlightString.Length;

            for (var i = 0; i < length; i++)
            {
                if (highlightString[i] == '*')
                {
                    // Check if it's a literal * (two consecutive *)
                    if (i + 1 < length && highlightString[i + 1] == '*')
                    {
                        // Two consecutive *'s represent a single literal *
                        if (isHighlighted)
                        {
                            highlightData.Add(actualIndex);
                        }
                        actualIndex++;
                        i++; // Skip the next *
                    }
                    else
                    {
                        isHighlighted = !isHighlighted;
                    }
                }
                else
                {
                    // Regular character
                    if (isHighlighted)
                    {
                        highlightData.Add(actualIndex);
                    }
                    actualIndex++;
                }
            }

            return highlightData;
        }
    }
}
