using System.Collections.Generic;
using System.Diagnostics;

namespace Flow.Launcher.Infrastructure
{
    /// <summary>
    /// Runs AppleScript through /usr/bin/osascript (macOS only).
    /// </summary>
    public static class AppleScript
    {
        private const string OsaScriptPath = "/usr/bin/osascript";

        /// <summary>osascript's exit code when the user dismisses a dialog with its cancel button ("User canceled", -128).</summary>
        public const int UserCanceledExitCode = 1;

        public readonly record struct Result(int ExitCode, string Output, string Error);

        /// <summary>
        /// Runs <paramref name="scriptLines"/> wrapped in <c>on run argv</c> and blocks until osascript exits.
        /// Untrusted text MUST be passed through <paramref name="arguments"/> (read as <c>item N of argv</c>)
        /// instead of being spliced into the script, so it never needs AppleScript string escaping.
        /// </summary>
        public static Result Run(IEnumerable<string> scriptLines, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo(OsaScriptPath)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-e");
            startInfo.ArgumentList.Add("on run argv");
            foreach (var line in scriptLines)
            {
                startInfo.ArgumentList.Add("-e");
                startInfo.ArgumentList.Add(line);
            }
            startInfo.ArgumentList.Add("-e");
            startInfo.ArgumentList.Add("end run");

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument ?? string.Empty);
            }

            using var process = Process.Start(startInfo);
            var errorTask = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return new Result(process.ExitCode, output.TrimEnd('\n', '\r'), errorTask.GetAwaiter().GetResult().TrimEnd('\n', '\r'));
        }
    }
}
