using System;

namespace Flow.Launcher.VimMode
{
    /// <summary>
    /// Provides pure static methods for calculating caret movements and text object selections.
    /// </summary>
    public class VimMotionEngine
    {
        /// <summary>
        /// Calculates the new caret position after moving left one character.
        /// </summary>
        /// <param name="caret">The current caret index.</param>
        /// <returns>The new caret index.</returns>
        public static int MoveLeft(int caret)
        {
            return Math.Max(0, caret - 1);
        }

        /// <summary>
        /// Calculates the new caret position after moving right one character.
        /// </summary>
        /// <param name="caret">The current caret index.</param>
        /// <param name="length">The total length of the text.</param>
        /// <returns>The new caret index.</returns>
        public static int MoveRight(int caret, int length)
        {
            return Math.Min(length, caret + 1);
        }

        /// <summary>
        /// Calculates the new caret position at the start of the line.
        /// </summary>
        /// <returns>The start index (always 0).</returns>
        public static int MoveStartOfLine()
        {
            return 0;
        }

        /// <summary>
        /// Calculates the new caret position at the end of the line.
        /// </summary>
        /// <param name="length">The total length of the text.</param>
        /// <returns>The end index.</returns>
        public static int MoveEndOfLine(int length)
        {
            return length;
        }

        /// <summary>
        /// Calculates the index of the first non-blank character in the text.
        /// </summary>
        /// <param name="text">The text to search.</param>
        /// <returns>The index of the first non-blank character, or 0 if none.</returns>
        public static int MoveFirstNonBlank(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsWhiteSpace(text[i])) return i;
            }
            return 0;
        }

        /// <summary>
        /// Calculates the index after the last non-blank character in the text.
        /// </summary>
        /// <param name="text">The text to search.</param>
        /// <returns>The target index.</returns>
        public static int MoveLastNonBlank(string text)
        {
            for (int i = text.Length - 1; i >= 0; i--)
            {
                if (!char.IsWhiteSpace(text[i])) return Math.Min(i + 1, text.Length);
            }
            return 0;
        }

        /// <summary>
        /// Calculates the index of the start of the next word (w motion).
        /// </summary>
        /// <param name="text">The text to traverse.</param>
        /// <param name="caret">The current caret index.</param>
        /// <returns>The new caret index.</returns>
        public static int MoveNextWord(string text, int caret)
        {
            if (caret >= text.Length) return text.Length;

            bool startIsWord = IsWordChar(text[caret]);
            int i = caret;

            while (i < text.Length && IsWordChar(text[i]) == startIsWord && !char.IsWhiteSpace(text[i]))
                i++;

            while (i < text.Length && char.IsWhiteSpace(text[i]))
                i++;

            return Math.Min(i, text.Length);
        }

        /// <summary>
        /// Calculates the index of the start of the previous word (b motion).
        /// </summary>
        /// <param name="text">The text to traverse.</param>
        /// <param name="caret">The current caret index.</param>
        /// <returns>The new caret index.</returns>
        public static int MovePrevWord(string text, int caret)
        {
            if (caret <= 0) return 0;

            int i = caret - 1;

            while (i > 0 && char.IsWhiteSpace(text[i]))
                i--;

            bool targetIsWord = IsWordChar(text[i]);

            while (i > 0 && IsWordChar(text[i - 1]) == targetIsWord && !char.IsWhiteSpace(text[i - 1]))
                i--;

            return Math.Max(0, i);
        }

        /// <summary>
        /// Calculates the index of the end of the current or next word (e motion).
        /// </summary>
        /// <param name="text">The text to traverse.</param>
        /// <param name="caret">The current caret index.</param>
        /// <returns>The new caret index.</returns>
        public static int MoveEndWord(string text, int caret)
        {
            if (caret >= text.Length - 1) return text.Length - 1;

            int i = caret + 1;

            while (i < text.Length && char.IsWhiteSpace(text[i]))
                i++;

            if (i >= text.Length) return caret;

            bool targetIsWord = IsWordChar(text[i]);

            while (i < text.Length - 1 && IsWordChar(text[i + 1]) == targetIsWord && !char.IsWhiteSpace(text[i + 1]))
                i++;

            return Math.Min(i, text.Length - 1);
        }

        /// <summary>
        /// Calculates the index of the start of the next big word (W motion).
        /// </summary>
        /// <param name="text">The text to traverse.</param>
        /// <param name="caret">The current caret index.</param>
        /// <returns>The new caret index.</returns>
        public static int MoveNextWordBig(string text, int caret)
        {
            if (caret >= text.Length) return text.Length;

            int i = caret;

            while (i < text.Length && !char.IsWhiteSpace(text[i]))
                i++;

            while (i < text.Length && char.IsWhiteSpace(text[i]))
                i++;

            return Math.Min(i, text.Length);
        }

        /// <summary>
        /// Calculates the index of the start of the previous big word (B motion).
        /// </summary>
        /// <param name="text">The text to traverse.</param>
        /// <param name="caret">The current caret index.</param>
        /// <returns>The new caret index.</returns>
        public static int MovePrevWordBig(string text, int caret)
        {
            if (caret <= 0) return 0;

            int i = caret - 1;

            while (i > 0 && char.IsWhiteSpace(text[i]))
                i--;

            while (i > 0 && !char.IsWhiteSpace(text[i - 1]))
                i--;

            return Math.Max(0, i);
        }

        /// <summary>
        /// Calculates the index of the end of the current or next big word (E motion).
        /// </summary>
        /// <param name="text">The text to traverse.</param>
        /// <param name="caret">The current caret index.</param>
        /// <returns>The new caret index.</returns>
        public static int MoveEndWordBig(string text, int caret)
        {
            if (caret >= text.Length - 1) return text.Length - 1;

            int i = caret + 1;

            while (i < text.Length && char.IsWhiteSpace(text[i]))
                i++;

            if (i >= text.Length) return caret;

            while (i < text.Length - 1 && !char.IsWhiteSpace(text[i + 1]))
                i++;

            return Math.Min(i, text.Length - 1);
        }

        /// <summary>
        /// Finds the index of the matching bracket (% motion).
        /// </summary>
        /// <param name="text">The text containing brackets.</param>
        /// <param name="caret">The current caret index.</param>
        /// <returns>The index of the matching bracket, or the original caret if none.</returns>
        public static int MoveToMatchingBracket(string text, int caret)
        {
            if (caret >= text.Length) return caret;
            char c = text[caret];

            char target;
            bool forward;
            switch (c)
            {
                case '(': target = ')'; forward = true; break;
                case ')': target = '('; forward = false; break;
                case '[': target = ']'; forward = true; break;
                case ']': target = '['; forward = false; break;
                case '{': target = '}'; forward = true; break;
                case '}': target = '{'; forward = false; break;
                default: return caret;
            }

            int depth = 0;
            char open = forward ? c : target;
            char close = forward ? target : c;

            if (forward)
            {
                for (int i = caret; i < text.Length; i++)
                {
                    if (text[i] == open) depth++;
                    else if (text[i] == close) { depth--; if (depth == 0) return i; }
                }
            }
            else
            {
                for (int i = caret; i >= 0; i--)
                {
                    if (text[i] == close) depth++;
                    else if (text[i] == open) { depth--; if (depth == 0) return i; }
                }
            }

            return caret;
        }

        /// <summary>
        /// Calculates the selection bounds for a word text object (iw / aw).
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="caret">The current caret index.</param>
        /// <param name="around">True for aw, false for iw.</param>
        /// <returns>A tuple containing the start and end indices of the selection.</returns>
        public static (int start, int end) TextObjectWord(string text, int caret, bool around)
        {
            if (text.Length == 0) return (0, 0);

            int start, end;
            int i = Math.Min(caret, text.Length - 1);
            bool isWs = char.IsWhiteSpace(text[i]);
            bool startIsWord = IsWordChar(text[i]);

            while (i > 0)
            {
                if (isWs)
                {
                    if (!char.IsWhiteSpace(text[i - 1])) break;
                }
                else
                {
                    if (IsWordChar(text[i - 1]) != startIsWord || char.IsWhiteSpace(text[i - 1])) break;
                }
                i--;
            }
            start = i;

            i = Math.Min(caret, text.Length - 1);
            while (i < text.Length - 1)
            {
                if (isWs)
                {
                    if (!char.IsWhiteSpace(text[i + 1])) break;
                }
                else
                {
                    if (IsWordChar(text[i + 1]) != startIsWord || char.IsWhiteSpace(text[i + 1])) break;
                }
                i++;
            }
            end = i;

            if (around && !isWs)
            {
                if (end + 1 < text.Length && char.IsWhiteSpace(text[end + 1]))
                {
                    while (end + 1 < text.Length && char.IsWhiteSpace(text[end + 1])) end++;
                }
                else if (start > 0 && char.IsWhiteSpace(text[start - 1]))
                {
                    while (start > 0 && char.IsWhiteSpace(text[start - 1])) start--;
                }
            }

            return (start, end);
        }

        /// <summary>
        /// Calculates the selection bounds for a delimited text object, such as parentheses or brackets.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="caret">The current caret index.</param>
        /// <param name="open">The opening delimiter.</param>
        /// <param name="close">The closing delimiter.</param>
        /// <param name="around">True to include the delimiters (a object), false to exclude (i object).</param>
        /// <returns>A tuple containing the start and end indices, or (-1, -1) if invalid.</returns>
                public static (int start, int end) TextObjectDelimited(string text, int caret, char open, char close, bool around)
        {
            int openPos = -1;
            int depth = 0;
            for (int i = Math.Min(caret, text.Length - 1); i >= 0; i--)
            {
                if (text[i] == close) depth++;
                else if (text[i] == open)
                {
                    if (depth == 0) { openPos = i; break; }
                    depth--;
                }
            }

            if (openPos == -1) return (-1, -1);

            int closePos = -1;
            depth = 0;
            for (int i = openPos + 1; i < text.Length; i++)
            {
                if (text[i] == open) depth++;
                else if (text[i] == close)
                {
                    if (depth == 0) { closePos = i; break; }
                    depth--;
                }
            }

            if (closePos == -1) return (-1, -1);

            int start = around ? openPos : openPos + 1;
            int end = around ? closePos : closePos - 1;

            if (start > end) return (start, start - 1);

            return (start, end);
        }

        /// <summary>
        /// Calculates the selection bounds for a quote text object.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="caret">The current caret index.</param>
        /// <param name="quote">The quote character.</param>
        /// <param name="around">True to include the quotes, false to exclude.</param>
        /// <returns>A tuple containing the start and end indices, or (-1, -1) if invalid.</returns>
                public static (int start, int end) TextObjectQuote(string text, int caret, char quote, bool around)
        {
            int first = -1;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == quote)
                {
                    if (first == -1) 
                    {
                        first = i;
                    }
                    else 
                    {
                        if (caret >= first && caret <= i)
                        {
                            int start = around ? first : first + 1;
                            int end = around ? i : i - 1;
                            if (start > end) return (start, start - 1);
                            return (start, end);
                        }
                        first = -1;
                    }
                }
            }

            return (-1, -1);
        }

        

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        /// <summary>
        /// Finds the next occurrence of a character (f / t motion).
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="caret">The current caret index.</param>
        /// <param name="target">The character to find.</param>
        /// <param name="till">True for t (stops before), false for f (lands on).</param>
        /// <returns>The index of the target character, or the original caret if not found.</returns>
        public static int FindCharForward(string text, int caret, char target, bool till = false)
        {
            if (caret >= text.Length - 1) return caret;
            int start = caret + 1;
            int index = text.IndexOf(target, start);
            if (index == -1) return caret;
            return till ? index - 1 : index;
        }

        /// <summary>
        /// Finds the previous occurrence of a character (F / T motion).
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="caret">The current caret index.</param>
        /// <param name="target">The character to find.</param>
        /// <param name="till">True for T (stops after), false for F (lands on).</param>
        /// <returns>The index of the target character, or the original caret if not found.</returns>
        public static int FindCharBackward(string text, int caret, char target, bool till = false)
        {
            if (caret <= 0) return caret;
            int start = caret - 1;
            int index = text.LastIndexOf(target, start);
            if (index == -1) return caret;
            return till ? index + 1 : index;
        }
    }
}
