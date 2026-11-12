using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;

namespace Flow.Launcher.Avalonia.Helper;

internal static partial class AutoStartup
{
    private const string LaunchAgentLabel = "com.flowlauncher.flowlauncher";

    private static readonly string LaunchAgentPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", $"{LaunchAgentLabel}.plist");

    // XDG Autostart: desktop sessions start every entry in $XDG_CONFIG_HOME/autostart at login.
    private static string LinuxAutostartPath => Path.Combine(LinuxDesktopEntry.ConfigHome, "autostart", LinuxDesktopEntry.FileName);

    private static string StartupFilePath => OperatingSystem.IsMacOS() ? LaunchAgentPath : LinuxAutostartPath;

    // macOS and Linux each have a single mechanism, so the logon task / registry choice does not matter.
    internal static partial void CheckIsEnabled(bool useLogonTaskForStartup)
    {
        EnsureSupported();

        try
        {
            var expected = BuildStartupFile();
            if (!File.Exists(StartupFilePath) || File.ReadAllText(StartupFilePath) != expected)
            {
                WriteStartupFile(expected);
            }
        }
        catch (Exception e)
        {
            App.API?.LogError(ClassName, $"Failed to check the startup entry: {e}");
            throw;
        }
    }

    internal static partial void DisableViaLogonTaskAndRegistry()
    {
        EnsureSupported();

        try
        {
            // File.Delete is a no-op when the file does not exist.
            File.Delete(StartupFilePath);
        }
        catch (Exception e)
        {
            App.API?.LogError(ClassName, $"Failed to disable auto-startup: {e}");
            throw;
        }
    }

    internal static partial void ChangeToViaLogonTask() => EnableStartupFile();

    internal static partial void ChangeToViaRegistry() => EnableStartupFile();

    private static void EnsureSupported()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Start at login is only supported on Windows, macOS and Linux.");
        }
    }

    private static void EnableStartupFile()
    {
        EnsureSupported();

        try
        {
            WriteStartupFile(BuildStartupFile());
        }
        catch (Exception e)
        {
            App.API?.LogError(ClassName, $"Failed to enable auto-startup: {e}");
            throw;
        }
    }

    /// <summary>
    /// The file is only written: launchd (macOS) and the desktop session (Linux) pick it up at the next login.
    /// </summary>
    private static void WriteStartupFile(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StartupFilePath)!);
        var tempPath = StartupFilePath + ".tmp";
        File.WriteAllText(tempPath, content, new UTF8Encoding(false));
        File.Move(tempPath, StartupFilePath, true);
    }

    private static string BuildStartupFile() =>
        OperatingSystem.IsMacOS() ? BuildLaunchAgentPlist() : LinuxDesktopEntry.Build(autostart: true);

    private static string BuildLaunchAgentPlist()
    {
        var arguments = string.Concat(GetLaunchArguments()
            .Select(a => $"\n\t\t<string>{SecurityElement.Escape(a)}</string>"));

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
            	<key>Label</key>
            	<string>{LaunchAgentLabel}</string>
            	<key>ProgramArguments</key>
            	<array>{arguments}
            	</array>
            	<key>RunAtLoad</key>
            	<true/>
            	<key>ProcessType</key>
            	<string>Interactive</string>
            	<key>LimitLoadToSessionType</key>
            	<string>Aqua</string>
            </dict>
            </plist>

            """;
    }

    /// <summary>
    /// The running executable: the apphost (inside the .app bundle on macOS, the install folder on Linux),
    /// or "dotnet Flow.Launcher.Avalonia.dll" when started through the muxer (development builds).
    /// </summary>
    internal static string[] GetLaunchArguments()
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot determine the path of the running executable.");

        return Path.GetFileNameWithoutExtension(processPath) == "dotnet"
            ? [processPath, typeof(App).Assembly.Location]
            : [processPath];
    }
}
