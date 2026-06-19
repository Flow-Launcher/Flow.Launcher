using System;

namespace Flow.Launcher.VimMode
{
    public class VimMotionEngine
    {
        public static int MoveLeft(int caret, int length)
        {
            return Math.Max(0, caret - 1);
        }

        public static int MoveRight(int caret, int length)
        {
            return Math.Min(length, caret + 1);
        }

        public static int MoveStartOfLine()
        {
            return 0;
        }

        public static int MoveEndOfLine(int length)
        {
            return length;
        }

        public static int MoveFirstNonBlank(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsWhiteSpace(text[i])) return i;
            }
            return 0;
        }

        public static int MoveLastNonBlank(string text)
        {
            for (int i = text.Length - 1; i >= 0; i--)
            {
                if (!char.IsWhiteSpace(text[i])) return Math.Min(i + 1, text.Length);
            }
            return 0;
        }

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

        public static int MoveEndWord(string text, int caret)
        {
            if (caret >= text.Length - 1) return text.Length;

            int i = caret + 1;

            while (i < text.Length - 1 && char.IsWhiteSpace(text[i]))
                i++;

            bool targetIsWord = IsWordChar(text[i]);

            while (i < text.Length - 1 && IsWordChar(text[i + 1]) == targetIsWord && !char.IsWhiteSpace(text[i + 1]))
                i++;

            return Math.Min(i, text.Length);
        }

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

        public static int MoveEndWordBig(string text, int caret)
        {
            if (caret >= text.Length - 1) return text.Length;

            int i = caret + 1;

            while (i < text.Length - 1 && char.IsWhiteSpace(text[i]))
                i++;

            while (i < text.Length - 1 && !char.IsWhiteSpace(text[i + 1]))
                i++;

            return Math.Min(i, text.Length);
        }

        public static int FindMatchingBracket(string text, int caret)
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

        public static (int start, int end) TextObjectWord(string text, int caret, bool around)
        {
            if (text.Length == 0) return (0, 0);

            int start, end;
            bool startIsWord = IsWordChar(text[Math.Min(caret, text.Length - 1)]);

            int i = Math.Min(caret, text.Length - 1);
            while (i > 0 && IsWordChar(text[i - 1]) == startIsWord && !char.IsWhiteSpace(text[i - 1]))
                i--;
            start = i;

            i = Math.Min(caret, text.Length - 1);
            while (i < text.Length - 1 && IsWordChar(text[i + 1]) == startIsWord && !char.IsWhiteSpace(text[i + 1]))
                i++;
            end = i;

            if (around)
            {
                if (end + 1 < text.Length && char.IsWhiteSpace(text[end + 1]))
                    end++;
                else if (start > 0 && char.IsWhiteSpace(text[start - 1]))
                    start--;
            }

            return (start, end);
        }

        public static (int start, int end) TextObjectDelimited(string text, int caret, char open, char close, bool around)
        {
            int depth = 0;
            int openPos = -1;
            int closePos = -1;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == open)
                {
                    if (i <= caret) { openPos = i; depth = 1; closePos = -1; }
                    else if (depth > 0) depth++;
                }
                else if (text[i] == close && depth > 0)
                {
                    depth--;
                    if (depth == 0) { closePos = i; break; }
                }
            }

            if (openPos == -1 || closePos == -1) return (-1, -1);

            int start = around ? openPos : openPos + 1;
            int end = around ? closePos : closePos - 1;

            return (start, end);
        }

        public static (int start, int end) TextObjectQuote(string text, int caret, char quote, bool around)
        {
            int first = -1, second = -1;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == quote)
                {
                    if (first == -1) first = i;
                    else if (second == -1) { second = i; break; }
                }
            }

            if (first == -1 || second == -1) return (-1, -1);
            if (caret < first || caret > second) return (-1, -1);

            int start = around ? first : first + 1;
            int end = around ? second : second - 1;

            return (start, end);
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        public static int FindCharForward(string text, int caret, char target, bool till = false)
        {
            if (caret >= text.Length - 1) return caret;
            int start = caret + 1;
            int index = text.IndexOf(target, start);
            if (index == -1) return caret;
            return till ? index - 1 : index;
        }

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
