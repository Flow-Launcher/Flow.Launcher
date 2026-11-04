#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Core.ExternalPlugins.PythonDependencies
{
    internal sealed class PythonRuntimeInstaller
    {
        private const string ExecutableName = "pythonw.exe";
        private const int ErrorLinesInMessage = 5;

        private static readonly TimeSpan DefaultProcessTimeout = TimeSpan.FromMinutes(15);

        private readonly UvLocator _uvLocator;
        private readonly IProcessRunner _runner;
        private readonly string _installDirectory;
        private readonly bool _portable;
        private readonly string _uvCachePath;
        private readonly TimeSpan _processTimeout;

        public PythonRuntimeInstaller(
            UvLocator uvLocator,
            IProcessRunner runner,
            string installDirectory,
            bool portable,
            string uvCachePath,
            TimeSpan? processTimeout = null)
        {
            _uvLocator = uvLocator;
            _runner = runner;
            _installDirectory = installDirectory;
            _portable = portable;
            _uvCachePath = uvCachePath;
            _processTimeout = processTimeout ?? DefaultProcessTimeout;
        }

        public static string ExecutablePath(string installDirectory)
        {
            return Path.Combine(installDirectory, UvPythonRuntime.InstallKey, ExecutableName);
        }

        public async Task<string> InstallAsync(CancellationToken token)
        {
            var uvPath = await LocateUvAsync(token).ConfigureAwait(false);
            var result = await RunUvAsync(uvPath, token).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                throw new PythonDependencyException(
                    $"uv failed to install Python {UvPythonRuntime.Version} (exit code {result.ExitCode}).{Environment.NewLine}{LastErrorLines(result.StdErr)}");
            }

            var executable = ExecutablePath(_installDirectory);
            if (!File.Exists(executable))
            {
                throw new PythonDependencyException($"uv finished but {executable} does not exist");
            }

            return executable;
        }

        private async Task<string> LocateUvAsync(CancellationToken token)
        {
            try
            {
                return await _uvLocator.GetUvPathAsync(token).ConfigureAwait(false);
            }
            catch (Exception e) when (!IsCallerCancellation(e, token))
            {
                throw new PythonDependencyException("Could not find or download uv to install Python", e);
            }
        }

        private async Task<ProcessResult> RunUvAsync(string uvPath, CancellationToken token)
        {
            var arguments = new[]
            {
                "python", "install", UvPythonRuntime.InstallKey,
                "--install-dir", _installDirectory,
                "--no-bin", "--no-registry", "--no-config"
            };

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(_processTimeout);
            try
            {
                return await _runner.RunAsync(uvPath, arguments, UvEnvironment.Build(_portable, _uvCachePath), timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException e) when (timeout.IsCancellationRequested && !token.IsCancellationRequested)
            {
                throw new PythonDependencyException($"uv did not finish installing Python within {_processTimeout}", e);
            }
            catch (Exception e) when (e is not PythonDependencyException && !IsCallerCancellation(e, token))
            {
                throw new PythonDependencyException($"Failed to run uv to install Python: {e.Message}", e);
            }
        }

        private static bool IsCallerCancellation(Exception exception, CancellationToken token)
        {
            return exception is OperationCanceledException && token.IsCancellationRequested;
        }

        private static string LastErrorLines(string stdErr)
        {
            var lines = stdErr
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .TakeLast(ErrorLinesInMessage);
            return string.Join(Environment.NewLine, lines);
        }
    }
}
