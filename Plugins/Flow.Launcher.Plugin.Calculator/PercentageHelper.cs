namespace Flow.Launcher.Plugin.Calculator;

/// <summary>
/// Rewrites percentage expressions into plain arithmetic before they are passed to Mages.
/// </summary>
/// <remarks>
/// Mages treats '%' as the modulo operator, so a '%' is only considered a percent sign when it is
/// not followed by an operand. For example "10%3" and "10 % (4)" stay modulo, while these are percentages:
/// <list type="bullet">
/// <item>"15%" becomes 0.15</item>
/// <item>"200*15%" becomes 200 * 0.15 = 30</item>
/// <item>"200+15%" becomes 200 + 15% of 200 = 230 (same as most handheld calculators)</item>
/// <item>"200-15%" becomes 200 - 15% of 200 = 170</item>
/// </list>
/// For '+' and '-' the base is everything to the left of the operator inside the same parentheses,
/// so "100+50+10%" is 165 and "100-10%-10%" is 81.
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
                // Nothing to take the percentage of (e.g. "%" or "(%)"), let Mages report the error.
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
        return next == expression.Length || expression[next] is ')' or ']' or ',' or '+' or '-' or '*' or '/' or '^' or '%';
    }

    private static string RewriteAt(string expression, int percentIndex)
    {
        var operandEnd = SkipWhiteSpaceBackward(expression, percentIndex - 1) + 1;
        var operandStart = FindOperandStart(expression, operandEnd - 1);
        if (operandStart == -1)
            return null;

        var operand = expression[operandStart..operandEnd];
        var rest = expression[(percentIndex + 1)..];

        var operatorIndex = SkipWhiteSpaceBackward(expression, operandStart - 1);
        if (operatorIndex >= 0 && expression[operatorIndex] is '+' or '-'
            && IsBinaryOperator(expression, operatorIndex)
            && EndsAdditiveTerm(expression, percentIndex))
        {
            var baseStart = FindAdditiveBaseStart(expression, operatorIndex - 1);
            var baseExpression = expression[baseStart..operatorIndex].Trim();
            if (baseExpression.Length > 0)
            {
                // a + b% -> a * (1 + b / 100)
                var op = expression[operatorIndex];
                return $"{expression[..baseStart]}({baseExpression})*(1{op}{operand}
            }
        }

        // b% -> (b / 100)
        return $"{expression[..operandStart]}({operand}/100){rest}";
    }

    /// <summary>
    /// Finds the start of the operand that ends at <paramref name="end"/>:
    /// a number, a constant like "pi", a parenthesized group or a function call lik
    /// </summary>
    private static int FindOperandStart(string expression, int end)
    {
        if (end < 0)
            return -1;

        var i = end;
        if (expression[i] is ')' or ']')
        {
            var depth = 0;
            for (; i >= 0; i--)
            {
                if (expression[i] is ')' or ']')
                    depth++;
                else if (expression[i] is '(' or '[' && --depth == 0)
                    break;
            }

            if (i < 0)
                return -1;

            // Include the function name, if any
            while (i > 0 && IsIdentifierChar(expression[i - 1]))
                i--;

            return i;
        }

        if (!IsIdentifierChar(expression[i]))
            return -1;

        while (i > 0 && IsIdentifierChar(expression[i - 1]))
            i--;

        return i;
    }

    /// <summary>
    /// Finds where the left side of a '+' or '-' begins: the start of the expressio
    /// the enclosing opening parenthesis, a function argument separator or a lower precedence operator.
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
                case ',' or ';' or '=' or '<' or '>' or '&' or '|' or '?' or ':' whe
                    return i + 1;
            }
        }

        return 0;
    }

    private static bool IsBinaryOperator(string expression, int operatorIndex)
    {
        var previous = SkipWhiteSpaceBackward(expression, operatorIndex - 1);
        return previous >= 0 && (IsIdentifierChar(expression[previous]) || expression[previous] is ')' or ']' or '!');
    }

    private static bool EndsAdditiveTerm(string expression, int percentIndex)
    {
        var next = SkipWhiteSpaceForward(expression, percentIndex + 1);
        return next == expression.Length || expression[next] is ')' or ']' or ',' or
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c is

    private static int SkipWhiteSpaceForward(string expression, int index)
    {
        while (index < expression.Length && char.IsWhiteSpace(expression[index]))
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
