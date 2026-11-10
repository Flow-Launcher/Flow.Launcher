using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Flow.Launcher.Infrastructure;

namespace Flow.Launcher.Core.ExternalPlugins.Environments
{
    public abstract partial class AbstractPluginEnvironment
    {
        private static partial string GetFileFromDialog(string title, string filter)
        {
            if (OperatingSystem.IsLinux())
            {
                return GetFileFromLinuxDialog(title);
            }

            if (!OperatingSystem.IsMacOS())
            {
                PublicApi.Instance.LogWarn(ClassName, "Runtime executable file dialog is not supported on this platform");
                return string.Empty;
            }

            try
            {
                // Runtimes usually live in hidden folders (/opt/homebrew/bin, ~/.pyenv); "with invisibles" shows them, Cmd+Shift+G jumps to a path.
                var result = AppleScript.Run(
                    ["activate", "return POSIX path of (choose file with prompt (item 1 of argv) with invisibles)"],
                    title);

                // Exit code 1 = user cancelled the panel.
                return result.ExitCode == 0 ? result.Output.Trim() : string.Empty;
            }
            catch (Exception e)
            {
                PublicApi.Instance.LogException(ClassName, "Failed to show runtime executable file dialog", e);
                return string.Empty;
            }
        }

        /// <summary>
        /// Core has no UI toolkit, so the picker comes from the desktop's dialog helper: zenity (GNOME and most others)
        /// or kdialog (KDE). Returns an empty string when cancelled or when neither is installed.
        /// </summary>
        private static string GetFileFromLinuxDialog(string title)
        {
            var directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            string FindOnPath(string name) => directories.Select(d => Path.Combine(d, name)).FirstOrDefault(File.Exists);

            ProcessStartInfo startInfo;
            if (FindOnPath("zenity") is { } zenity)
            {
                startInfo = new ProcessStartInfo(zenity);
                startInfo.ArgumentList.Add("--file-selection");
                startInfo.ArgumentList.Add($"--title={title}");
            }
            else if (FindOnPath("kdialog") is { } kdialog)
            {
                startInfo = new ProcessStartInfo(kdialog);
                startInfo.ArgumentList.Add("--title");
                startInfo.ArgumentList.Add(title);
                startInfo.ArgumentList.Add("--getopenfilename");
                startInfo.ArgumentList.Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            }
            else
            {
                PublicApi.Instance.LogWarn(ClassName, "Neither zenity nor kdialog is installed; enter the runtime path manually");
                return string.Empty;
            }

            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            try
            {
                using var process = Process.Start(startInfo);
                _ = process.StandardError.ReadToEndAsync();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // Exit code 1 = user cancelled the dialog.
                return process.ExitCode == 0 ? output.Trim() : string.Empty;
            }
            catch (Exception e)
            {
                PublicApi.Instance.LogException(ClassName, "Failed to show runtime executable file dialog", e);
                return string.Empty;
            }
        }

        /// <summary>
        /// Locates a runtime installed on the system (Flow cannot download one here): the user's login-shell PATH first
        /// (pyenv/asdf/Homebrew setups), then common install folders, then the given extra folders.
        /// Returns null when none is found.
        /// </summary>
        protected string FindSystemRuntime(string executableName, params string[] extraDirectories)
        {
            var fromShell = FindWithLoginShell(executableName);
            if (fromShell != null)
            {
                return fromShell;
            }

            // Apps started from Finder/launchd get a minimal PATH, so also probe the usual install folders.
            var directories = new List<string> { "/opt/homebrew/bin", "/usr/local/bin" };
            directories.AddRange((Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
            directories.AddRange(extraDirectories);

            return directories
                .Select(directory => Path.Combine(directory, executableName))
                .FirstOrDefault(File.Exists);
        }

        private string FindWithLoginShell(string executableName)
        {
            try
            {
                var shell = Environment.GetEnvironmentVariable("SHELL");
                if (string.IsNullOrEmpty(shell) || !File.Exists(shell))
                {
                    shell = OperatingSystem.IsMacOS() ? "/bin/zsh" : "/bin/sh";
                }

                var startInfo = new ProcessStartInfo(shell)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("-l");
                startInfo.ArgumentList.Add("-c");
                startInfo.ArgumentList.Add($"command -v {executableName}");

                using var process = Process.Start(startInfo);
                process.StandardInput.Close();
                _ = process.StandardError.ReadToEndAsync();
                var outputTask = process.StandardOutput.ReadToEndAsync();
                if (!process.WaitForExit(TimeSpan.FromSeconds(5)))
                {
                    process.Kill(entireProcessTree: true);
                    return null;
                }

                // Profile scripts may print banners; the path is the last line.
                var candidate = outputTask.GetAwaiter().GetResult()
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .LastOrDefault();

                return candidate != null && Path.IsPathRooted(candidate) && File.Exists(candidate) ? candidate : null;
            }
            catch (Exception e)
            {
                API.LogException(ClassName, $"Failed to look up {executableName} in the login shell", e);
                return null;
            }
        }
    }
}
