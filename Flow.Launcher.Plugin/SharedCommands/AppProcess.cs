using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;

namespace Flow.Launcher.Plugin.SharedCommands
{
    /// <summary>
    /// Starts programs on the user's behalf (apps, files, URLs, terminals), as opposed to helper processes Flow or a
    /// plugin talks to. Under a systemd user session on Linux each program gets its own transient scope
    /// (<c>app-flowlauncher-&lt;app&gt;-&lt;random&gt;.scope</c>, as <c>uwsm app</c> and other launchers do), so it is
    /// accounted and killed separately and outlives Flow: stopping, restarting or crashing Flow's own unit
    /// (e.g. its XDG autostart service) would otherwise kill everything started from it. Elsewhere this is
    /// <see cref="Process.Start(ProcessStartInfo)"/>.
    /// </summary>
    public static class AppProcess
    {
        private const string Launcher = "flowlauncher";

        private static readonly Lazy<string> SystemdRun = new(FindSystemdRun);

        /// <summary>
        /// Starts the program described by <paramref name="startInfo"/>; see <see cref="AppProcess"/>.
        /// </summary>
        /// <exception cref="Win32Exception">The program could not be started, e.g. it does not exist.</exception>
        public static Process Start(ProcessStartInfo startInfo) => Start(startInfo, null);

        /// <summary>
        /// Starts the program described by <paramref name="startInfo"/>; see <see cref="AppProcess"/>.
        /// </summary>
        /// <param name="startInfo">The program to start. Programs with redirected standard streams are started
        /// directly, without a scope.</param>
        /// <param name="applicationId">Names the scope, preferably the desktop entry id (without ".desktop"); defaults
        /// to the program's file name.</param>
        /// <exception cref="Win32Exception">The program could not be started, e.g. it does not exist.</exception>
        public static Process Start(ProcessStartInfo startInfo, string applicationId)
        {
            ArgumentNullException.ThrowIfNull(startInfo);

            if (!OperatingSystem.IsLinux() || SystemdRun.Value is not { } systemdRun
                || startInfo.RedirectStandardInput || startInfo.RedirectStandardOutput || startInfo.RedirectStandardError)
            {
                return Process.Start(startInfo);
            }

            var command = ResolveCommand(startInfo);
            var scoped = new ProcessStartInfo(systemdRun)
            {
                UseShellExecute = false,
                WorkingDirectory = startInfo.WorkingDirectory,
            };

            // ProcessStartInfo.Environment starts as a copy of Flow's; carry over what the caller changed.
            scoped.Environment.Clear();
            foreach (var (key, value) in startInfo.Environment)
            {
                scoped.Environment[key] = value;
            }

            scoped.ArgumentList.Add("--user");
            scoped.ArgumentList.Add("--scope");
            scoped.ArgumentList.Add("--quiet");
            scoped.ArgumentList.Add("--collect");
            var unitName = UnitName(applicationId ?? Path.GetFileName(command[0]));
            scoped.ArgumentList.Add($"--unit={unitName}");
            if (GraphicalAppSliceExists())
            {
                scoped.ArgumentList.Add("--slice=app-graphical.slice");
            }

            scoped.ArgumentList.Add("--");
            foreach (var argument in command)
            {
                scoped.ArgumentList.Add(argument);
            }

            // systemd-run creates the scope for its own process and then execs the program, so the returned process
            // is the program itself.
            var process = Process.Start(scoped);
            WaitUntilInScope(process, unitName);
            return process;
        }

        private static readonly TimeSpan ScopeTimeout = TimeSpan.FromSeconds(2);

        // The move into the scope completes a few milliseconds after the start; until then the process is still in
        // Flow's cgroup, and Flow's unit stopping in that window (Flow exiting right after a launch, as RestartApp
        // does) would kill it. Waits until /proc/<pid>/cgroup names the scope, or the process is gone.
        private static void WaitUntilInScope(Process process, string unitName)
        {
            if (process is null)
            {
                return;
            }

            var cgroupFile = $"/proc/{process.Id}/cgroup";
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < ScopeTimeout)
            {
                try
                {
                    if (File.ReadAllText(cgroupFile).TrimEnd().EndsWith("/" + unitName, StringComparison.Ordinal))
                    {
                        return;
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    return; // exited (e.g. systemd-run failed)
                }

                Thread.Sleep(5);
            }
        }

        // The argv the program runs with: the resolved executable and its arguments. Shell-execute starts of
        // non-executables go through xdg-open, as Process.Start does on Linux.
        [SupportedOSPlatform("linux")]
        private static List<string> ResolveCommand(ProcessStartInfo startInfo)
        {
            var arguments = startInfo.ArgumentList.Count > 0
                ? startInfo.ArgumentList.ToList()
                : SplitArguments(startInfo.Arguments);

            if (FindExecutable(startInfo.FileName, startInfo.WorkingDirectory) is { } executable)
            {
                return [executable, .. arguments];
            }

            if (startInfo.UseShellExecute && FindExecutable("xdg-open", null) is { } opener)
            {
                return [opener, startInfo.FileName];
            }

            // The failure Process.Start reports, raised before a scope is created for a program that cannot run.
            throw new Win32Exception(2, $"An error occurred trying to start process '{startInfo.FileName}'. No such file or directory");
        }

        [SupportedOSPlatform("linux")]
        private static string FindExecutable(string fileName, string workingDirectory)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            if (fileName.Contains('/'))
            {
                var path = Path.GetFullPath(fileName, string.IsNullOrEmpty(workingDirectory) ? Environment.CurrentDirectory : workingDirectory);
                return IsExecutableFile(path) ? path : null;
            }

            return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(directory => Path.Combine(directory, fileName))
                .FirstOrDefault(IsExecutableFile);
        }

        [SupportedOSPlatform("linux")]
        private static bool IsExecutableFile(string path) =>
            File.Exists(path)
            && (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;

        // systemd-run talks to the user manager through $XDG_RUNTIME_DIR/systemd/private; without it (no systemd
        // user session, e.g. in containers) programs are started directly.
        private static string FindSystemdRun()
        {
            if (!OperatingSystem.IsLinux()
                || Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is not { Length: > 0 } runtimeDirectory
                || !File.Exists(Path.Combine(runtimeDirectory, "systemd", "private")))
            {
                return null;
            }

            return FindExecutable("systemd-run", null);
        }

        // uwsm sessions put graphical apps into app-graphical.slice; plain systemd sessions use the default app.slice.
        private static bool GraphicalAppSliceExists()
        {
            var uid = GetUid();
            return uid != null && Directory.Exists(
                $"/sys/fs/cgroup/user.slice/user-{uid}.slice/user@{uid}.service/app.slice/app-graphical.slice");
        }

        private static string GetUid()
        {
            // /proc/self/status: "Uid:\t<real>\t<effective>\t..."
            foreach (var line in File.ReadLines("/proc/self/status"))
            {
                if (line.StartsWith("Uid:", StringComparison.Ordinal))
                {
                    return line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1);
                }
            }

            return null;
        }

        // app-<launcher>-<ApplicationID>-<RANDOM>.scope (systemd's "Desktop Environment Integration"); the id is
        // escaped like systemd-escape does, so '-' in it does not read as a separator, and shortened to keep the
        // name well below systemd's 255 character limit.
        private static string UnitName(string applicationId)
        {
            var escaped = new StringBuilder();
            foreach (var b in Encoding.UTF8.GetBytes(applicationId.Length > 64 ? applicationId[..64] : applicationId))
            {
                var c = (char)b;
                if (char.IsAsciiLetterOrDigit(c) || c is ':' or '_' || (c == '.' && escaped.Length > 0))
                {
                    escaped.Append(c);
                }
                else
                {
                    escaped.Append($"\\x{b:x2}");
                }
            }

            return $"app-{Launcher}-{escaped}-{Random.Shared.Next():x8}.scope";
        }

        // Splits a ProcessStartInfo.Arguments string the way .NET does for the argv of a Unix process: whitespace
        // separates arguments, double quotes group, and backslashes escape quotes (Windows command-line rules).
        private static List<string> SplitArguments(string arguments)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(arguments))
            {
                return result;
            }

            var current = new StringBuilder();
            var inQuotes = false;
            var hasArgument = false;
            for (var i = 0; i < arguments.Length; i++)
            {
                var c = arguments[i];
                if (c == '\\')
                {
                    var backslashes = 0;
                    while (i < arguments.Length && arguments[i] == '\\')
                    {
                        backslashes++;
                        i++;
                    }

                    if (i < arguments.Length && arguments[i] == '"')
                    {
                        // 2n backslashes + quote: n backslashes, quote toggles; 2n+1: n backslashes and a literal quote
                        current.Append('\\', backslashes / 2);
                        if (backslashes % 2 == 1)
                        {
                            current.Append('"');
                        }
                        else
                        {
                            inQuotes = !inQuotes;
                        }
                    }
                    else
                    {
                        current.Append('\\', backslashes);
                        i--;
                    }

                    hasArgument = true;
                }
                else if (c == '"')
                {
                    inQuotes = !inQuotes;
                    hasArgument = true;
                }
                else if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (hasArgument)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                        hasArgument = false;
                    }
                }
                else
                {
                    current.Append(c);
                    hasArgument = true;
                }
            }

            if (hasArgument)
            {
                result.Add(current.ToString());
            }

            return result;
        }
    }
}
