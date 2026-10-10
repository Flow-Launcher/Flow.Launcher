using System;
using System.Diagnostics;
using System.IO;

namespace Flow.Launcher.Plugin.Shell
{
#pragma warning disable FLAN0005 // The plugin context property is declared in Main.cs
    public partial class Main
#pragma warning restore FLAN0005
    {
        /// <summary>
        /// cmd, PowerShell and pwsh are Windows shells; outside Windows every managed shell runs the command
        /// through the user's login shell. "Leave shell open" and "close after key press" need a visible
        /// terminal, which is Terminal.app on macOS. Windows Terminal does not exist here.
        /// </summary>
        private static partial void ConfigureShellProcessStartInfo(
            ProcessStartInfo info,
            string command,
            Shell shell,
            bool leaveShellOpen,
            bool closeShellAfterPress,
            bool useWindowsTerminal,
            string closePrompt)
        {
            info.UseShellExecute = false;
            info.CreateNoWindow = true;

            if ((leaveShellOpen || closeShellAfterPress) && OperatingSystem.IsMacOS())
            {
                // Terminal.app types the script into a new window running the user's interactive shell,
                // which stays open afterwards unless the script exits it.
                var script = closeShellAfterPress
                    ? $"{command}; echo {QuoteForShell(closePrompt)}; /bin/bash -c 'read -rsn 1'; exit"
                    : command;

                info.FileName = "/usr/bin/osascript";
                info.ArgumentList.Add("-e");
                info.ArgumentList.Add("tell application \"Terminal\"");
                info.ArgumentList.Add("-e");
                info.ArgumentList.Add($"do script \"{EscapeForAppleScript(script)}\"");
                info.ArgumentList.Add("-e");
                info.ArgumentList.Add("activate");
                info.ArgumentList.Add("-e");
                info.ArgumentList.Add("end tell");
                return;
            }

            // Login shell so PATH additions from the user's profile (e.g. Homebrew) apply,
            // since apps started from Finder get a minimal PATH.
            info.FileName = GetUserShell();
            info.ArgumentList.Add("-l");
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add(command);
        }

        /// <summary>
        /// Running as administrator is Windows-only; .NET on Unix rejects shell execute verbs other than "open",
        /// so the "runas" verb set for RunCommand and custom templates must not be passed on.
        /// </summary>
        static partial void RemoveRunAsVerb(ProcessStartInfo info)
        {
            info.Verb = string.Empty;
        }

        internal static string GetUserShell()
        {
            var shell = Environment.GetEnvironmentVariable("SHELL");
            if (!string.IsNullOrWhiteSpace(shell) && File.Exists(shell))
            {
                return shell;
            }

            return OperatingSystem.IsMacOS() ? "/bin/zsh" : "/bin/sh";
        }

        private static string QuoteForShell(string value)
        {
            return $"'{value.Replace("'", "'\\''")}'";
        }

        private static string EscapeForAppleScript(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
