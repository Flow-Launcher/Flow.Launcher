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
