#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Core.ExternalPlugins.PythonDependencies
{
    internal sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);

    internal interface IProcessRunner
    {
        Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string> environment,
            CancellationToken token);
    }

    internal sealed class ProcessRunner : IProcessRunner
    {
        public async Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string> environment,
            CancellationToken token)
        {
            var startInfo = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            foreach (var (name, value) in environment)
            {
                startInfo.Environment[name] = value;
            }

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            using var cancellation = token.Register(() => TryKill(process));

            var stdOut = process.StandardOutput.ReadToEndAsync();
            var stdErr = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(stdOut, stdErr).ConfigureAwait(false);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

            token.ThrowIfCancellationRequested();
            return new ProcessResult(process.ExitCode, stdOut.Result, stdErr.Result);
        }

        private static void TryKill(Process process)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // The process already exited.
            }
        }
    }
}
