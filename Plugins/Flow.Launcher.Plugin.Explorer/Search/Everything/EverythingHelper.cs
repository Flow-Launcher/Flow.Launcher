using System;
using System.Collections.Generic;
using System.Text;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    public enum EverythingStateCode
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

    public static class EverythingHelper
    {
        #region Query

        public readonly record struct PreparedQuery(
            EverythingSearchOption Option,
            string SearchText);

        public static PreparedQuery PrepareQuery(EverythingSearchOption option)
        {
            if (option.Offset < 0)
                throw new ArgumentOutOfRangeException(nameof(option.Offset), option.Offset, "Offset must be greater than or equal to 0");
            if (option.MaxCount < 0)
                throw new ArgumentOutOfRangeException(nameof(option.MaxCount), option.MaxCount, "MaxCount must be greater than or equal to 0");

            var keyword = option.Keyword;
            if (!string.IsNullOrEmpty(keyword) && keyword.StartsWith("@", StringComparison.Ordinal))
            {
                option.UseRegex = true;
                keyword = keyword[1..];
            }

            var builder = new StringBuilder();
            builder.Append(keyword);

            if (!string.IsNullOrWhiteSpace(option.ParentPath))
            {
                builder.Append($" {(option.IsRecursive ? "" : "parent:")}\"{option.ParentPath}\"");
            }

            if (option.IsContentSearch)
            {
                builder.Append($" content:\"{option.ContentSearchKeyword}\"");
            }

            return new PreparedQuery(option with { Keyword = keyword }, builder.ToString());
        }

        #endregion

        #region Result

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

        #endregion
    }
}
