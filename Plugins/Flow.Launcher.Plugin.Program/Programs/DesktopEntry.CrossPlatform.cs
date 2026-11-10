using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Flow.Launcher.Plugin.Program.Programs
{
    /// <summary>
    /// The [Desktop Entry] group of a freedesktop .desktop file
    /// (https://specifications.freedesktop.org/desktop-entry-spec/latest/).
    /// </summary>
    internal sealed class DesktopEntry
    {
        private const string DesktopEntryGroup = "Desktop Entry";

        private readonly Dictionary<string, string> _values;

        public string Path { get; }

        private DesktopEntry(string path, Dictionary<string, string> values)
        {
            Path = path;
            _values = values;
        }

        /// <summary>
        /// Reads the [Desktop Entry] group; null if the file has none.
        /// </summary>
        public static DesktopEntry Load(string path)
        {
            var groups = KeyFile.Read(path, DesktopEntryGroup);
            return groups.TryGetValue(DesktopEntryGroup, out var values) ? new DesktopEntry(path, values) : null;
        }

        /// <summary>
        /// Unescaped value of a string key, or null if missing or blank.
        /// </summary>
        public string GetString(string key)
        {
            return _values.TryGetValue(key, out var raw) ? NonBlank(Unescape(raw)) : null;
        }

        /// <summary>
        /// Value of a localestring key for the first matching locale in <paramref name="locales"/>
        /// (most specific first), falling back to the unlocalized key.
        /// </summary>
        public string GetLocaleString(string key, IReadOnlyList<string> locales)
        {
            foreach (var locale in locales)
            {
                if (_values.TryGetValue($"{key}[{locale}]", out var raw) && NonBlank(Unescape(raw)) is { } value)
                    return value;
            }

            return GetString(key);
        }

        public bool GetBoolean(string key)
        {
            return _values.TryGetValue(key, out var raw) && raw.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Values of a ';'-separated list key; empty if missing.
        /// </summary>
        public IReadOnlyList<string> GetStrings(string key)
        {
            return _values.TryGetValue(key, out var raw) ? SplitList(raw) : Array.Empty<string>();
        }

        /// <summary>
        /// Localized list (e.g. Keywords[de]) for the first matching locale, or the unlocalized list.
        /// </summary>
        public IReadOnlyList<string> GetLocaleStrings(string key, IReadOnlyList<string> locales)
        {
            foreach (var locale in locales)
            {
                if (_values.TryGetValue($"{key}[{locale}]", out var raw))
                {
                    var values = SplitList(raw);
                    if (values.Count > 0)
                        return values;
                }
            }

            return GetStrings(key);
        }

        /// <summary>
        /// Locale names to try for localized keys, most specific first, following the spec's
        /// lang_COUNTRY@MODIFIER, lang_COUNTRY, lang@MODIFIER, lang matching order.
        /// </summary>
        public static IReadOnlyList<string> GetLocaleCandidates(CultureInfo culture)
        {
            string language = null, country = null, modifier = null;

            if (!string.IsNullOrEmpty(culture?.Name))
            {
                // .NET names look like "de-DE", "zh-TW", "sr-Latn-RS" or "es-419"
                var parts = culture.Name.Split('-');
                language = parts[0];
                foreach (var part in parts.Skip(1))
                {
                    if (part.Length == 4)
                        modifier = part.Equals("Latn", StringComparison.OrdinalIgnoreCase) ? "latin" : null;
                    else
                        country = part.ToUpperInvariant();
                }
            }
            else
            {
                // Invariant culture: fall back to the POSIX locale, e.g. "de_DE.UTF-8@euro"
                var posix = new[] { "LC_ALL", "LC_MESSAGES", "LANG" }
                    .Select(Environment.GetEnvironmentVariable)
                    .FirstOrDefault(v => !string.IsNullOrEmpty(v) && v != "C" && v != "POSIX");
                if (posix == null)
                    return Array.Empty<string>();

                var at = posix.IndexOf('@');
                if (at >= 0)
                {
                    modifier = posix[(at + 1)..];
                    posix = posix[..at];
                }

                var dot = posix.IndexOf('.');
                if (dot >= 0)
                    posix = posix[..dot];

                var underscore = posix.IndexOf('_');
                language = underscore >= 0 ? posix[..underscore] : posix;
                country = underscore >= 0 ? posix[(underscore + 1)..] : null;
            }

            if (string.IsNullOrEmpty(language))
                return Array.Empty<string>();

            var candidates = new List<string>(4);
            if (country != null && modifier != null)
                candidates.Add($"{language}_{country}@{modifier}");
            if (country != null)
                candidates.Add($"{language}_{country}");
            if (modifier != null)
                candidates.Add($"{language}@{modifier}");
            candidates.Add(language);
            return candidates;
        }

        /// <summary>
        /// Splits the Exec key into arguments following the spec's quoting rules, before field code expansion.
        /// </summary>
        public IReadOnlyList<string> GetExecArguments()
        {
            var exec = GetString("Exec");
            return exec == null ? Array.Empty<string>() : SplitCommandLine(exec);
        }

        /// <summary>
        /// Builds the command line to launch the entry without files or URLs: %f %F %u %U are dropped,
        /// %i becomes "--icon &lt;Icon&gt;", %c the (localized) name, %k the desktop file path and %% a literal %.
        /// </summary>
        public List<string> ExpandExec(string name, IReadOnlyList<string> locales)
        {
            var icon = GetLocaleString("Icon", locales);
            var arguments = new List<string>();

            foreach (var argument in GetExecArguments())
            {
                if (argument == "%i")
                {
                    if (icon != null)
                    {
                        arguments.Add("--icon");
                        arguments.Add(icon);
                    }

                    continue;
                }

                var expanded = new StringBuilder(argument.Length);
                var droppedFieldCode = false;
                for (var i = 0; i < argument.Length; i++)
                {
                    if (argument[i] != '%' || i == argument.Length - 1)
                    {
                        expanded.Append(argument[i]);
                        continue;
                    }

                    var code = argument[++i];
                    switch (code)
                    {
                        case '%':
                            expanded.Append('%');
                            break;
                        case 'c':
                            expanded.Append(name);
                            break;
                        case 'k':
                            expanded.Append(Path);
                            break;
                        case 'i':
                            expanded.Append(icon);
                            break;
                        default:
                            // %f %F %u %U: no files to pass; %d %D %n %N %v %m: deprecated; anything else: invalid
                            droppedFieldCode = true;
                            break;
                    }
                }

                // A standalone %U disappears instead of becoming an empty argument
                if (expanded.Length > 0 || !droppedFieldCode)
                    arguments.Add(expanded.ToString());
            }

            return arguments;
        }

        /// <summary>
        /// Splits a command line into arguments. Arguments are separated by unquoted spaces or tabs. Inside double
        /// quotes a backslash escapes ", `, $ and \ (as the spec requires); like GLib's g_shell_parse_argv, single
        /// quotes and backslashes outside quotes are honoured too because real-world files rely on them.
        /// </summary>
        public static List<string> SplitCommandLine(string commandLine)
        {
            var arguments = new List<string>();
            var current = new StringBuilder();
            var inArgument = false;

            for (var i = 0; i < commandLine.Length; i++)
            {
                var c = commandLine[i];
                switch (c)
                {
                    case ' ' or '\t' or '\n':
                        if (inArgument)
                        {
                            arguments.Add(current.ToString());
                            current.Clear();
                            inArgument = false;
                        }

                        break;
                    case '"':
                        inArgument = true;
                        for (i++; i < commandLine.Length && commandLine[i] != '"'; i++)
                        {
                            if (commandLine[i] == '\\' && i + 1 < commandLine.Length &&
                                commandLine[i + 1] is '"' or '`' or '$' or '\\')
                            {
                                i++;
                            }

                            current.Append(commandLine[i]);
                        }

                        break;
                    case '\'':
                        inArgument = true;
                        for (i++; i < commandLine.Length && commandLine[i] != '\''; i++)
                        {
                            current.Append(commandLine[i]);
                        }

                        break;
                    case '\\' when i + 1 < commandLine.Length:
                        inArgument = true;
                        current.Append(commandLine[++i]);
                        break;
                    default:
                        inArgument = true;
                        current.Append(c);
                        break;
                }
            }

            if (inArgument)
                arguments.Add(current.ToString());

            return arguments;
        }

        /// <summary>
        /// Resolves the string-level escapes \s \n \t \r \\. Other backslashes are kept so the Exec quoting rules
        /// (e.g. \" and \$ inside quotes) can still see them.
        /// </summary>
        private static string Unescape(string value)
        {
            if (!value.Contains('\\'))
                return value;

            var result = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] != '\\' || i == value.Length - 1)
                {
                    result.Append(value[i]);
                    continue;
                }

                switch (value[i + 1])
                {
                    case 's':
                        result.Append(' ');
                        break;
                    case 'n':
                        result.Append('\n');
                        break;
                    case 't':
                        result.Append('\t');
                        break;
                    case 'r':
                        result.Append('\r');
                        break;
                    case '\\':
                        result.Append('\\');
                        break;
                    default:
                        result.Append('\\').Append(value[i + 1]);
                        break;
                }

                i++;
            }

            return result.ToString();
        }

        private static List<string> SplitList(string raw)
        {
            var values = new List<string>();
            var current = new StringBuilder();

            for (var i = 0; i < raw.Length; i++)
            {
                if (raw[i] == '\\' && i + 1 < raw.Length && raw[i + 1] == ';')
                {
                    current.Append(';');
                    i++;
                }
                else if (raw[i] == ';')
                {
                    AddValue();
                }
                else
                {
                    current.Append(raw[i]);
                }
            }

            AddValue();
            return values;

            void AddValue()
            {
                if (NonBlank(Unescape(current.ToString())) is { } value)
                    values.Add(value);
                current.Clear();
            }
        }

        private static string NonBlank(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    /// <summary>
    /// Minimal reader for the INI-like "key file" format shared by .desktop files, icon theme index.theme files,
    /// GTK settings.ini and kdeglobals. The first occurrence of a group or key wins.
    /// </summary>
    internal static class KeyFile
    {
        /// <param name="path">File to read.</param>
        /// <param name="onlyGroup">If set, stop after this group so large files aren't parsed needlessly.</param>
        public static Dictionary<string, Dictionary<string, string>> Read(string path, string onlyGroup = null)
        {
            var groups = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            Dictionary<string, string> current = null;

            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                    continue;

                if (line[0] == '[' && line[^1] == ']')
                {
                    if (onlyGroup != null && groups.ContainsKey(onlyGroup))
                        break;

                    var name = line[1..^1];
                    if (groups.ContainsKey(name))
                    {
                        // Duplicate group: ignore its keys
                        current = null;
                        continue;
                    }

                    current = new Dictionary<string, string>(StringComparer.Ordinal);
                    if (onlyGroup == null || name == onlyGroup)
                        groups[name] = current;
                    continue;
                }

                var separator = line.IndexOf('=');
                if (current == null || separator <= 0)
                    continue;

                current.TryAdd(line[..separator].TrimEnd(), line[(separator + 1)..].TrimStart());
            }

            return groups;
        }
    }
}
