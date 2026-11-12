using System;
using System.Collections.Generic;
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
        /// terminal, which is Terminal.app on macOS and the user's terminal emulator on Linux.
        /// Windows Terminal does not exist here.
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

            if ((leaveShellOpen || closeShellAfterPress) && OperatingSystem.IsLinux()
                && FindLinuxTerminal() is { } terminal)
            {
                // Unlike Terminal.app, a terminal emulator closes when its command exits, so leaving it open
                // means replacing the finished command with an interactive shell.
                var shellPath = GetUserShell();
                var script = closeShellAfterPress
                    ? $"{command}; echo {QuoteForShell(closePrompt)}; {GetReadKeyCommand()}"
                    : $"{command}; exec {QuoteForShell(shellPath)}";

                info.FileName = terminal.Path;
                foreach (var argument in terminal.ExecArguments)
                {
                    info.ArgumentList.Add(argument);
                }
                info.ArgumentList.Add(shellPath);
                info.ArgumentList.Add("-c");
                info.ArgumentList.Add(script);
                return;
            }

            if ((leaveShellOpen || closeShellAfterPress) && OperatingSystem.IsLinux())
            {
                Context?.API.LogWarn(ClassName, "No terminal emulator found on PATH; running the command without a terminal");
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

        /// <summary>
        /// A terminal emulator and the arguments that make it run the command line following them.
        /// </summary>
        internal sealed record LinuxTerminal(string Path, IReadOnlyList<string> ExecArguments);

        /// <summary>
        /// Known terminal emulators in order of preference, with the option introducing the command to run.
        /// x-terminal-emulator (Debian alternatives) must support -e; xdg-terminal-exec runs the user's
        /// configured default terminal.
        /// </summary>
        private static readonly (string Name, string[] ExecArguments)[] LinuxTerminals =
        [
            ("x-terminal-emulator", ["-e"]),
            ("xdg-terminal-exec", []),
            ("kitty", []),
            ("alacritty", ["-e"]),
            ("foot", []),
            ("wezterm", ["start", "--"]),
            ("ghostty", ["-e"]),
            ("ptyxis", ["--"]),
            ("kgx", ["--"]),
            ("gnome-terminal", ["--"]),
            ("konsole", ["-e"]),
            ("xfce4-terminal", ["-x"]),
            ("xterm", ["-e"])
        ];

        /// <summary>
        /// $TERMINAL first (the de facto variable for the preferred terminal, often set on tiling window managers),
        /// then the known terminals found on PATH. Returns null when there is none.
        /// </summary>
        internal static LinuxTerminal FindLinuxTerminal()
        {
            // $TERMINAL may carry options of its own, e.g. "kitty --single-instance"
            var preferred = (Environment.GetEnvironmentVariable("TERMINAL") ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (preferred.Length > 0 && FindExecutable(preferred[0]) is { } preferredPath)
            {
                var name = Path.GetFileName(preferredPath);
                var execArguments = new List<string>(preferred[1..]);
                // Terminals not in the list are expected to follow the xterm convention
                execArguments.AddRange(Array.Find(LinuxTerminals, t => t.Name == name).ExecArguments ?? ["-e"]);
                return new LinuxTerminal(preferredPath, execArguments);
            }

            foreach (var (name, execArguments) in LinuxTerminals)
            {
                if (FindExecutable(name) is { } path)
                {
                    return new LinuxTerminal(path, execArguments);
                }
            }

            return null;
        }

        /// <summary>
        /// Waits for a single key press. read -n is a bash extension, so run it through bash when it exists;
        /// POSIX sh can only wait for Enter.
        /// </summary>
        private static string GetReadKeyCommand() =>
            FindExecutable("bash") is { } bash
                ? $"{QuoteForShell(bash)} -c 'read -rsn 1'"
                : "sh -c 'read -r _'";

        /// <summary>
        /// Resolves a command name through PATH like the shell does (distributions such as NixOS keep
        /// nothing in /usr/bin); paths are taken as they are.
        /// </summary>
        private static string FindExecutable(string name)
        {
            if (name.Contains('/'))
            {
                return IsExecutableFile(name) ? name : null;
            }

            foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(directory, name);
                if (IsExecutableFile(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsExecutableFile(string path)
        {
            try
            {
                return File.Exists(path)
                    && (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return false;
            }
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
