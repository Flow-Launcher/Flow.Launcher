using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.Calculator;

internal static partial class MainRegexHelper
{
    [GeneratedRegex(@"-?[\d\.,]+", RegexOptions.Compiled)]
    public static partial Regex GetNumberRegex();

    [GeneratedRegex(@"\B(?=(\d{3})+(?!\d))", RegexOptions.Compiled)]
    public static partial Regex GetThousandGroupRegex();

    [GeneratedRegex(@"\bpow(\((?:[^()\[\]]|\((?<Depth>)|\)(?<-Depth>)|\[(?<Depth>)|\](?<-Depth>))*(?(Depth)(?!))\))", RegexOptions.Compiled | RegexOptions.RightToLeft | RegexOptions.IgnoreCase)]
    public static partial Regex GetPowRegex();

    [GeneratedRegex(@"\b(sqrt|pow|factorial|abs|sign|ceil|floor|round|exp|log|log2|log10|min|max|lt|eq|gt|sin|cos|tan|arcsin|arccos|arctan|isnan|isint|isprime|isinfty|rand|randi|type|is|as|length|throw|catch|eval|map|clamp|lerp|regex|shuffle)\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    public static partial Regex GetFunctionRegex();
}
