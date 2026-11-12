using System;
using System.IO;
using System.Linq;
using System.Text;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// The freedesktop desktop entry of the running Flow Launcher. Desktops use it to name and decorate the app's windows
/// (matched through the X11 WM_CLASS, which is the app id), XDG autostart starts it at login, and the portal only
/// accepts the app id of an installed entry when Flow registers for global shortcuts.
/// </summary>
internal static class LinuxDesktopEntry
{
    private static readonly string ClassName = nameof(LinuxDesktopEntry);

    internal const string AppId = Program.LinuxAppId;

    internal const string FileName = AppId + ".desktop";

    // Marks entries Flow wrote itself, so they are refreshed when the app moves but packaged or user-edited ones are kept.
    private const string GeneratedKey = "X-Flow-Launcher-Generated";

    private static string DataHome => XdgDirectory("XDG_DATA_HOME", ".local", "share");

    internal static string ConfigHome => XdgDirectory("XDG_CONFIG_HOME", ".config");

    private static string XdgDirectory(string variable, params string[] fallback) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value && Path.IsPathRooted(value)
            ? value
            : Path.Combine([Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), .. fallback]);

    /// <summary>
    /// Writes <c>$XDG_DATA_HOME/applications/com.flowlauncher.FlowLauncher.desktop</c> for the running executable unless
    /// an entry with this id is installed already (by a package, or edited by the user). Returns true when it wrote the file.
    /// </summary>
    internal static bool EnsureInstalled()
    {
        try
        {
            var path = Path.Combine(DataHome, "applications", FileName);
            var expected = Build(autostart: false);
            if (File.Exists(path))
            {
                var current = File.ReadAllText(path);
                if (current == expected || !current.Contains($"{GeneratedKey}=true", StringComparison.Ordinal))
                {
                    return false;
                }
            }
            else if (DataDirectories().Any(directory => File.Exists(Path.Combine(directory, "applications", FileName))))
            {
                return false;
            }

            Write(path, expected);
            Log.Info(ClassName, $"Installed desktop entry {path}");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Warn(ClassName, $"Cannot install the desktop entry: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// The desktop entry starting the running executable; <paramref name="autostart"/> builds the XDG autostart variant.
    /// </summary>
    internal static string Build(bool autostart)
    {
        var exec = string.Join(' ', AutoStartup.GetLaunchArguments().Select(QuoteExecArgument));
        var icon = Path.Combine(AppContext.BaseDirectory, "Images", "app.png");

        var builder = new StringBuilder()
            .Append("[Desktop Entry]\n")
            .Append("Type=Application\n")
            .Append("Name=Flow Launcher\n")
            .Append("GenericName=Launcher\n")
            .Append("Comment=Quick file search and app launcher\n")
            .Append("Exec=").Append(EscapeValue(exec)).Append('\n')
            .Append("Icon=").Append(EscapeValue(icon)).Append('\n')
            .Append("Terminal=false\n")
            .Append("Categories=Utility;\n")
            .Append("Keywords=launcher;search;run;\n")
            .Append("StartupWMClass=").Append(AppId).Append('\n');

        if (autostart)
        {
            builder.Append("X-GNOME-Autostart-enabled=true\n");
        }

        return builder.Append(GeneratedKey).Append("=true\n").ToString();
    }

    internal static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, content, new UTF8Encoding(false));
        File.Move(tempPath, path, true);
    }

    private static string[] DataDirectories() =>
        (Environment.GetEnvironmentVariable("XDG_DATA_DIRS") is { Length: > 0 } dirs ? dirs : "/usr/local/share:/usr/share")
        .Split(':', StringSplitOptions.RemoveEmptyEntries);

    // Exec quoting (Desktop Entry spec, "The Exec key"): arguments with reserved characters are double-quoted, and
    // ", `, $ and \ inside quotes are backslash-escaped; a literal % is written as %%.
    private static string QuoteExecArgument(string argument)
    {
        argument = argument.Replace("%", "%%");
        if (argument.Length > 0 && argument.IndexOfAny(" \t\n\"'\\><~|&;$*?#()`".ToCharArray()) < 0)
        {
            return argument;
        }

        var quoted = new StringBuilder("\"");
        foreach (var c in argument)
        {
            if (c is '"' or '`' or '$' or '\\')
            {
                quoted.Append('\\');
            }

            quoted.Append(c);
        }

        return quoted.Append('"').ToString();
    }

    // String values escape backslashes and control characters (Desktop Entry spec, "Possible value types").
    private static string EscapeValue(string value) =>
        value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\t", "\\t").Replace("\r", "\\r");
}
