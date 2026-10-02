namespace Flow.Launcher.Plugin.Calculator;

/// <summary>
/// Rewrites percentages into plain arithmetic before they reach Mages.
/// </summary>
/// <remarks>
/// Mages treats '%' as modulo, so '%' is a percent sign only when no
/// operand follows it. "10%3" and "10 % (4)" stay modulo, while:
/// <list type="bullet">
/// <item>"15%" is 0.15</item>
/// <item>"200*15%" is 200 * 0.15 = 30</item>
/// <item>"200+15%" is 200 + 15% of 200 = 230</item>
/// <item>"200-15%" is 200 - 15% of 200 = 170</item>
/// </list>
/// For '+' and '-' the base is everything to the left of the operator
/// inside the same parentheses, so "100+50+10%" is 165
/// and "100-10%-10%" is 81.
/// </remarks>
internal static class PercentageHelper
{
    public static string Rewrite(string expression)
    {
        var start = 0;
        int index;
        while ((index = FindPercentSign(expression, start)) != -1)
        {
            var rewritten = RewriteAt(expression, index);
            if (rewritten == null)
            {
                // Nothing to take the percentage of, e.g. "%" or "(%)".
                // Leave it for Mages to report the error.
                start = index + 1;
                continue;
            }

            expression = rewritten;
            start = 0;
        }

        return expression;
    }

    private static int FindPercentSign(string expression, int start)
    {
        for (var i = start; i < expression.Length; i++)
        {
            if (expression[i] == '%' && IsPercentSign(expression, i))
                return i;
        }

        return -1;
    }

    private static bool IsPercentSign(string expression, int index)
    {
        var next = SkipWhiteSpaceForward(expression, index + 1);
        return next == expression.Length
            || expression[next] is ')' or ']' or ',' or '%'
                or '+' or '-' or '*' or '/' or '^';
    }

    private static string RewriteAt(string expression, int percentIndex)
    {
        var operandEnd =
            SkipWhiteSpaceBackward(expression, percentIndex - 1) + 1;
        var operandStart = FindOperandStart(expression, operandEnd - 1);
        if (operandStart == -1)
            return null;

        var operand = expression[operandStart..operandEnd];
        var head = expression[..operandStart];
        var rest = expression[(percentIndex + 1)..];

        if (EndsAdditiveTerm(expression, percentIndex))
        {
            var additive = RewriteAdditive(
                expression, operandStart, operand, rest);
            if (additive != null)
                return additive;
        }

        // b% -> (b / 100)
        return $"{head}({operand}/100){rest}";
    }

    /// <summary>
    /// Rewrites "a + b%" to "(a)*(1+b/100)" and "a - b%" to
    /// "(a)*(1-b/100)". Returns null if there is no "a +" or "a -".
    /// </summary>
    private static string RewriteAdditive(
        string expression, int operandStart, string operand, string rest)
    {
        var opIndex = SkipWhiteSpaceBackward(expression, operandStart - 1);

        // A sign in front of the percentage belongs to it,
        // e.g. "100+-10%" is 100 + (-10%) = 90.
        if (IsSign(expression, opIndex)
            && !IsBinaryOperator(expression, opIndex))
        {
            operand = $"({expression[opIndex]}{operand})";
            opIndex = SkipWhiteSpaceBackward(expression, opIndex - 1);
        }

        if (!IsSign(expression, opIndex)
            || !IsBinaryOperator(expression, opIndex))
            return null;

        var baseStart = FindAdditiveBaseStart(expression, opIndex - 1);
        var baseExpression = expression[baseStart..opIndex].Trim();
        if (baseExpression.Length == 0)
            return null;

        var head = expression[..baseStart];
        var op = expression[opIndex];
        return $"{head}({baseExpression})*(1{op}{operand}/100){rest}";
    }

    /// <summary>
    /// Finds the start of the operand that ends at <paramref name="end"/>:
    /// a number, a constant like "pi", a group in parentheses
    /// or a function call like "sqrt(16)" or "sqrt (16)".
    /// </summary>
    private static int FindOperandStart(string expression, int end)
    {
        if (end < 0)
            return -1;

        if (expression[end] is not (')' or ']'))
        {
            if (!IsIdentifierChar(expression[end]))
                return -1;

            return FindIdentifierStart(expression, end);
        }

        var open = FindOpeningBracket(expression, end);
        if (open == -1)
            return -1;

        // Include the function name, if any
        var nameEnd = SkipWhiteSpaceBackward(expression, open - 1);
        if (nameEnd >= 0 && IsIdentifierChar(expression[nameEnd]))
        {
            var nameStart = FindIdentifierStart(expression, nameEnd);
            if (char.IsLetter(expression[nameStart]))
                return nameStart;
        }

        return open;
    }

    private static int FindOpeningBracket(string expression, int close)
    {
        var depth = 0;
        for (var i = close; i >= 0; i--)
        {
            if (expression[i] is ')' or ']')
                depth++;
            else if (expression[i] is '(' or '[' && --depth == 0)
                return i;
        }

        return -1;
    }

    private static int FindIdentifierStart(string expression, int end)
    {
        var i = end;
        while (i > 0 && IsIdentifierChar(expression[i - 1]))
            i--;
        return i;
    }

    /// <summary>
    /// Finds where the left side of a '+' or '-' begins: the start of the
    /// expression, the enclosing opening parenthesis, a function argument
    /// separator or an operator with lower precedence.
    /// </summary>
    private static int FindAdditiveBaseStart(string expression, int end)
    {
        var depth = 0;
        for (var i = end; i >= 0; i--)
        {
            switch (expression[i])
            {
                case ')' or ']':
                    depth++;
                    break;
                case '(' or '[':
                    if (depth == 0)
                        return i + 1;
                    depth--;
                    break;
                case ',' or ';' or '=' or '<' or '>'
                    or '&' or '|' or '?' or ':' when depth == 0:
                    return i + 1;
            }
        }

        return 0;
    }

    private static bool IsSign(string expression, int index) =>
        index >= 0 && expression[index] is '+' or '-';

    private static bool IsBinaryOperator(string expression, int index)
    {
        var previous = SkipWhiteSpaceBackward(expression, index - 1);
        return previous >= 0
            && (IsIdentifierChar(expression[previous])
                || expression[previous] is ')' or ']' or '!');
    }

    private static bool EndsAdditiveTerm(string expression, int index)
    {
        var next = SkipWhiteSpaceForward(expression, index + 1);
        return next == expression.Length
            || expression[next] is ')' or ']' or ',' or '+' or '-';
    }

    private static bool IsIdentifierChar(char c) =>
        char.IsLetterOrDigit(c) || c is '.' or '_';

    private static int SkipWhiteSpaceForward(string expression, int index)
    {
        while (index < expression.Length
               && char.IsWhiteSpace(expression[index]))
            index++;
        return index;
    }

    private static int SkipWhiteSpaceBackward(string expression, int index)
    {
        while (index >= 0 && char.IsWhiteSpace(expression[index]))
            index--;
        return index;
    }
}
