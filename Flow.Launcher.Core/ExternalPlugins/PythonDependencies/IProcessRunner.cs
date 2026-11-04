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
        private static readonly TimeSpan KillGracePeriod = TimeSpan.FromSeconds(10);

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

            var stdOut = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var stdErr = process.StandardError.ReadToEndAsync(CancellationToken.None);
            try
            {
                await Task.WhenAll(stdOut, stdErr).WaitAsync(token).ConfigureAwait(false);
                await process.WaitForExitAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // A process that survives the kill, or a child still holding the output pipes, must not
                // hold up the caller past its timeout.
                await WaitBrieflyForExitAsync(process).ConfigureAwait(false);
                IgnoreFaults(stdOut);
                IgnoreFaults(stdErr);
                throw;
            }

            return new ProcessResult(process.ExitCode, stdOut.Result, stdErr.Result);
        }

        private static async Task WaitBrieflyForExitAsync(Process process)
        {
            try
            {
                await process.WaitForExitAsync().WaitAsync(KillGracePeriod).ConfigureAwait(false);
            }
            catch (Exception e) when (e is TimeoutException or InvalidOperationException)
            {
                // Give up on it; uv installs into a fresh folder each time, so a leftover process is harmless.
            }
        }

        private static void IgnoreFaults(Task task)
        {
            _ = task.ContinueWith(static t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }

        private static void TryKill(Process process)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (AggregateException)
            {
                // Part of the tree could not be killed. Make sure the process itself goes.
                TryKillProcess(process);
            }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // The process already exited.
            }
        }

        private static void TryKillProcess(Process process)
        {
            try
            {
                process.Kill();
            }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // The process already exited.
            }
        }
    }
}
