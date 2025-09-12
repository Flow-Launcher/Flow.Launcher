using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.Calculator;

internal static partial class MainRegexHelper
{

    [GeneratedRegex(@"[\(\)\[\]]", RegexOptions.Compiled)]
    public static partial Regex GetRegBrackets();

    [GeneratedRegex(@"^(ceil|floor|exp|pi|e|max|min|det|abs|log|ln|sqrt|sin|cos|tan|arcsin|arccos|arctan|eigval|eigvec|eig|sum|polar|plot|round|sort|real|zeta|bin2dec|hex2dec|oct2dec|factorial|sign|isprime|isinfty|==|~=|&&|\|\||(?:\<|\>)=?|[ei]|[0-9]|0x[\da-fA-F]+|[\+\%\-\*\/\^\., ""]|[\(\)\|\!\[\]])+$", RegexOptions.Compiled)]
    public static partial Regex GetRegValidExpressChar();

    [GeneratedRegex(@"-?[\d\.,]+", RegexOptions.Compiled)]
    public static partial Regex GetNumberRegex();

    [GeneratedRegex(@"\B(?=(\d{3})+(?!\d))", RegexOptions.Compiled)]
    public static partial Regex GetThousandGroupRegex();
}
