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

    // macOS has a single mechanism, so the logon task / registry choice does not matter.
    internal static partial void CheckIsEnabled(bool useLogonTaskForStartup)
    {
        EnsureSupported();

        try
        {
            var expected = BuildLaunchAgentPlist();
            if (!File.Exists(LaunchAgentPath) || File.ReadAllText(LaunchAgentPath) != expected)
            {
                WriteLaunchAgent(expected);
            }
        }
        catch (Exception e)
        {
            App.API?.LogError(ClassName, $"Failed to check launch agent: {e}");
            throw;
        }
    }

    internal static partial void DisableViaLogonTaskAndRegistry()
    {
        EnsureSupported();

        try
        {
            // File.Delete is a no-op when the file does not exist.
            File.Delete(LaunchAgentPath);
        }
        catch (Exception e)
        {
            App.API?.LogError(ClassName, $"Failed to disable auto-startup: {e}");
            throw;
        }
    }

    internal static partial void ChangeToViaLogonTask() => EnableLaunchAgent();

    internal static partial void ChangeToViaRegistry() => EnableLaunchAgent();

    private static void EnsureSupported()
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("Start at login is only supported on Windows and macOS.");
        }
    }

    private static void EnableLaunchAgent()
    {
        EnsureSupported();

        try
        {
            WriteLaunchAgent(BuildLaunchAgentPlist());
        }
        catch (Exception e)
        {
            App.API?.LogError(ClassName, $"Failed to enable auto-startup: {e}");
            throw;
        }
    }

    /// <summary>
    /// The plist is written (not loaded via launchctl): launchd picks it up at the next login.
    /// </summary>
    private static void WriteLaunchAgent(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LaunchAgentPath)!);
        var tempPath = LaunchAgentPath + ".tmp";
        File.WriteAllText(tempPath, content, new UTF8Encoding(false));
        File.Move(tempPath, LaunchAgentPath, true);
    }

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
    /// The running executable: the apphost inside the .app bundle (Contents/MacOS/Flow.Launcher.Avalonia),
    /// or "dotnet Flow.Launcher.Avalonia.dll" when started through the muxer (development builds).
    /// </summary>
    private static string[] GetLaunchArguments()
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot determine the path of the running executable.");

        return Path.GetFileNameWithoutExtension(processPath) == "dotnet"
            ? [processPath, typeof(App).Assembly.Location]
            : [processPath];
    }
}
