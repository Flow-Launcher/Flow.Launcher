using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.Calculator;

internal static partial class MainRegexHelper
{
    [GeneratedRegex(@"[\(\)\[\]]", RegexOptions.Compiled)]
    public static partial Regex GetRegBrackets();

    [GeneratedRegex(@"-?[\d\.,]+", RegexOptions.Compiled)]
    public static partial Regex GetNumberRegex();

    [GeneratedRegex(@"\B(?=(\d{3})+(?!\d))", RegexOptions.Compiled)]
    public static partial Regex GetThousandGroupRegex();

    [GeneratedRegex(@"\bpow(\((?:[^()\[\]]|\((?<Depth>)|\)(?<-Depth>)|\[(?<Depth>)|\](?<-Depth>))*(?(Depth)(?!))\))", RegexOptions.Compiled | RegexOptions.RightToLeft)]
    public static partial Regex GetPowRegex();
}
