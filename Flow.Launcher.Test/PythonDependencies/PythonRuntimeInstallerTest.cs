using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Core.ExternalPlugins.PythonDependencies;
using Flow.Launcher.Infrastructure.UserSettings;
using NUnit.Framework;

namespace Flow.Launcher.Test.PythonDependencies
{
    [TestFixture]
    public class PythonRuntimeInstallerTest
    {
        private static readonly string InstallKey = UvPythonRuntime.InstallKey;

        private string tempDirectory;
        private string installDirectory;
        private string cachePath;
        private FakeRunner runner;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "FlowRuntimeInstallerTest_" + Guid.NewGuid().ToString("N"));
            installDirectory = Path.Combine(tempDirectory, "install dir", "Runtime");
            cachePath = Path.Combine(tempDirectory, "uv cache");
            Directory.CreateDirectory(tempDirectory);
            runner = new FakeRunner(installDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        private UvLocator CreateLocator(Func<string> findOnPath = null)
        {
            return new UvLocator(
                new PluginsSettings(),
                Path.Combine(tempDirectory, "uv"),
                _ => false,
                findOnPath ?? (() => "uv"),
                (_, _) => throw new InvalidOperationException("download should not be used"));
        }

        private PythonRuntimeInstaller CreateInstaller(
            bool portable = false,
            UvLocator locator = null,
            TimeSpan? processTimeout = null,
            Func<string> proxyUrl = null)
        {
            return new PythonRuntimeInstaller(
                locator ?? CreateLocator(),
                runner,
                installDirectory,
                portable,
                cachePath,
                processTimeout,
                proxyUrl);
        }

        [Test]
        public void Pin_MatchesTheRuntimeFlowShips()
        {
            Assert.That(UvPythonRuntime.Version, Is.EqualTo("3.11.17"));
            Assert.That(UvPythonRuntime.InstallDirectoryName, Is.EqualTo("Runtime"));
        }

        [TestCase(Architecture.X64, "cpython-3.11.17-windows-x86_64-none")]
        [TestCase(Architecture.Arm64, "cpython-3.11.17-windows-x86_64-none")]
        [TestCase(Architecture.X86, "cpython-3.11.17-windows-x86-none")]
        public void InstallKey_FollowsArchitecture(Architecture architecture, string expected)
        {
            Assert.That(UvPythonRuntime.InstallKeyFor(architecture), Is.EqualTo(expected));
        }

        [Test]
        public void IsInstalledIn_OnlyMatchesExecutablesInsideTheRuntimeFolder()
        {
            var runtime = Path.Combine(tempDirectory, "Python", "Runtime");

            Assert.That(UvPythonRuntime.IsInstalledIn(Path.Combine(runtime, InstallKey, "pythonw.exe"), runtime), Is.True);
            Assert.That(UvPythonRuntime.IsInstalledIn(Path.Combine(runtime + "Other", "pythonw.exe"), runtime), Is.False);
            Assert.That(UvPythonRuntime.IsInstalledIn(Path.Combine(tempDirectory, "Python", "pythonw.exe"), runtime), Is.False);
        }

        [Test]
        public async Task Install_RunsUvPythonInstall_WithInstallDirectoryAsOneArgument()
        {
            await CreateInstaller().InstallAsync(CancellationToken.None);

            var call = runner.Calls.Single();
            Assert.That(call.FileName, Is.EqualTo("uv"));
            Assert.That(call.Arguments, Is.EqualTo(new[]
            {
                "python", "install", InstallKey,
                "--install-dir", installDirectory,
                "--no-bin", "--no-registry", "--no-config"
            }));
            Assert.That(installDirectory, Does.Contain(" "));
        }

        [Test]
        public async Task Install_NotPortable_SetsOnlyNoConfig()
        {
            await CreateInstaller(portable: false).InstallAsync(CancellationToken.None);

            Assert.That(runner.Calls.Single().Environment, Is.EqualTo(new Dictionary<string, string> { ["UV_NO_CONFIG"] = "1" }));
        }

        [Test]
        public async Task Install_WithProxy_PassesItToUv()
        {
            await CreateInstaller(proxyUrl: () => "http://proxy.local:8080").InstallAsync(CancellationToken.None);

            var environment = runner.Calls.Single().Environment;
            Assert.That(environment["HTTP_PROXY"], Is.EqualTo("http://proxy.local:8080"));
            Assert.That(environment["HTTPS_PROXY"], Is.EqualTo("http://proxy.local:8080"));
        }

        [Test]
        public async Task Install_Portable_SetsCacheDirectory()
        {
            await CreateInstaller(portable: true).InstallAsync(CancellationToken.None);

            Assert.That(runner.Calls.Single().Environment, Is.EqualTo(new Dictionary<string, string>
            {
                ["UV_NO_CONFIG"] = "1",
                ["UV_CACHE_DIR"] = cachePath
            }));
        }

        [Test]
        public async Task Install_ReturnsPythonwInsideTheInstallKeyFolder()
        {
            var path = await CreateInstaller().InstallAsync(CancellationToken.None);

            Assert.That(path, Is.EqualTo(Path.Combine(installDirectory, InstallKey, "pythonw.exe")));
            Assert.That(path, Is.EqualTo(PythonRuntimeInstaller.ExecutablePath(installDirectory)));
            Assert.That(File.Exists(path), Is.True);
        }

        [Test]
        public void UvFails_ThrowsWithLastFiveStderrLines()
        {
            runner.ExitCode = 2;
            runner.StdErr = "one\r\ntwo\r\n\r\nthree\r\nfour\r\nfive\r\nsix\r\nerror: download failed\r\n";

            var exception = Assert.ThrowsAsync<PythonDependencyException>(() =>
                CreateInstaller().InstallAsync(CancellationToken.None));

            Assert.That(exception.Message, Does.Contain("error: download failed"));
            Assert.That(exception.Message, Does.Contain("three"));
            Assert.That(exception.Message, Does.Not.Contain("two"));
        }

        [Test]
        public void PythonwMissingAfterSuccess_Throws()
        {
            runner.CreateExecutable = false;

            var exception = Assert.ThrowsAsync<PythonDependencyException>(() =>
                CreateInstaller().InstallAsync(CancellationToken.None));

            Assert.That(exception.Message, Does.Contain("pythonw.exe"));
        }

        [Test]
        public void UvCannotBeLocated_ThrowsAndRunsNothing()
        {
            var locator = CreateLocator(findOnPath: () => null);

            var exception = Assert.ThrowsAsync<PythonDependencyException>(() =>
                CreateInstaller(locator: locator).InstallAsync(CancellationToken.None));

            Assert.That(exception.InnerException, Is.Not.Null);
            Assert.That(runner.Calls, Is.Empty);
        }

        [Test]
        public void UvHangs_TimesOut_Throws()
        {
            runner.Delay = TimeSpan.FromMinutes(5);

            var exception = Assert.ThrowsAsync<PythonDependencyException>(() =>
                CreateInstaller(processTimeout: TimeSpan.FromMilliseconds(50)).InstallAsync(CancellationToken.None));

            Assert.That(exception.InnerException, Is.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public void CallerCancels_PropagatesOperationCanceledException()
        {
            runner.Delay = TimeSpan.FromMinutes(5);
            using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            Assert.CatchAsync<OperationCanceledException>(() => CreateInstaller().InstallAsync(source.Token));
        }

        [Test]
        public void RunnerThrows_WrapsInPythonDependencyException()
        {
            runner.Failure = new InvalidOperationException("cannot start uv");

            var exception = Assert.ThrowsAsync<PythonDependencyException>(() =>
                CreateInstaller().InstallAsync(CancellationToken.None));

            Assert.That(exception.Message, Does.Contain("cannot start uv"));
        }

        private sealed record Call(string FileName, IReadOnlyList<string> Arguments, IReadOnlyDictionary<string, string> Environment);

        private sealed class FakeRunner : IProcessRunner
        {
            private readonly string installDirectory;
            private readonly List<Call> calls = new();

            public FakeRunner(string installDirectory)
            {
                this.installDirectory = installDirectory;
            }

            public int ExitCode { get; set; }
            public string StdErr { get; set; } = string.Empty;
            public bool CreateExecutable { get; set; } = true;
            public TimeSpan Delay { get; set; } = TimeSpan.Zero;
            public Exception Failure { get; set; }

            public IReadOnlyList<Call> Calls => calls.ToList();

            public async Task<ProcessResult> RunAsync(
                string fileName,
                IReadOnlyList<string> arguments,
                IReadOnlyDictionary<string, string> environment,
                CancellationToken token)
            {
                calls.Add(new Call(fileName, arguments.ToList(), new Dictionary<string, string>(environment)));

                if (Delay > TimeSpan.Zero)
                {
                    await Task.Delay(Delay, token);
                }

                if (Failure is not null)
                {
                    throw Failure;
                }

                if (ExitCode == 0 && CreateExecutable)
                {
                    var executable = PythonRuntimeInstaller.ExecutablePath(installDirectory);
                    Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
                    File.WriteAllText(executable, string.Empty);
                }

                return new ProcessResult(ExitCode, string.Empty, StdErr);
            }
        }
    }
}
