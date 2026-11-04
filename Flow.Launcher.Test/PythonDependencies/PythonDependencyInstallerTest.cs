using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Core.ExternalPlugins.PythonDependencies;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using NUnit.Framework;

namespace Flow.Launcher.Test.PythonDependencies
{
    [TestFixture]
    public class PythonDependencyInstallerTest
    {
        private const string PluginId = "plugin-id-1";
        private const string PluginName = "Test Plugin";
        private const string PythonVersionProbeOutput = "3.11.9 AMD64";

        private string tempDirectory;
        private string dependenciesRoot;
        private string cachePath;
        private string pythonPath;
        private PluginsSettings settings;
        private FakeProcessRunner runner;
        private int uvLocateCount;
        private List<string> logged;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "FlowDependencyInstallerTest_" + Guid.NewGuid().ToString("N"));
            dependenciesRoot = Path.Combine(tempDirectory, "deps root");
            cachePath = Path.Combine(tempDirectory, "uv cache");
            pythonPath = Path.Combine(tempDirectory, "py", "python.exe");
            Directory.CreateDirectory(tempDirectory);
            settings = new PluginsSettings();
            runner = new FakeProcessRunner();
            uvLocateCount = 0;
            logged = new List<string>();
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
                settings,
                Path.Combine(tempDirectory, "uv"),
                _ => false,
                () =>
                {
                    Interlocked.Increment(ref uvLocateCount);
                    return findOnPath is null ? "uv" : findOnPath();
                },
                (_, _) => throw new InvalidOperationException("download should not be used"));
        }

        private PythonDependencyInstaller CreateInstaller(
            FakeProcessRunner processRunner = null,
            UvLocator locator = null,
            bool portable = false)
        {
            return new PythonDependencyInstaller(
                settings,
                locator ?? CreateLocator(),
                processRunner ?? runner,
                dependenciesRoot,
                portable,
                cachePath,
                logged.Add);
        }

        private static PluginMetadata CreateMetadata(params string[] dependencies)
        {
            return new PluginMetadata
            {
                ID = PluginId,
                Name = PluginName,
                Dependencies = dependencies.ToList()
            };
        }

        private string KeyFor(params string[] dependencies)
        {
            return DependencyInstallMarker.ComputeKey(
                DependencyRequirements.Normalize(dependencies), pythonPath, PythonVersionProbeOutput);
        }

        private string SitePackagesFor(string key) => Path.Combine(dependenciesRoot, PluginId, key, "site-packages");

        private string MarkerPath => Path.Combine(dependenciesRoot, PluginId, DependencyInstallMarker.FileName);

        [Test]
        public async Task EmptyDependencies_ReturnsNull_NoProcessRuns()
        {
            var installer = CreateInstaller();

            var result = await installer.EnsureInstalledAsync(CreateMetadata(), pythonPath, CancellationToken.None);

            Assert.That(result, Is.Null);
            Assert.That(runner.Calls, Is.Empty);
            Assert.That(uvLocateCount, Is.EqualTo(0));
        }

        [Test]
        public async Task NullDependencies_ReturnsNull_NoProcessRuns()
        {
            var metadata = CreateMetadata();
            metadata.Dependencies = null;
            var installer = CreateInstaller();

            var result = await installer.EnsureInstalledAsync(metadata, pythonPath, CancellationToken.None);

            Assert.That(result, Is.Null);
            Assert.That(runner.Calls, Is.Empty);
        }

        [Test]
        public void UnpinnedAndNotAllowed_ThrowsPythonDependencyException_NoUvCall()
        {
            settings.AllowUnpinnedPythonDependencies = false;
            var installer = CreateInstaller();

            var exception = Assert.ThrowsAsync<PythonDependencyException>(() =>
                installer.EnsureInstalledAsync(CreateMetadata("requests>=2"), pythonPath, CancellationToken.None));

            Assert.That(exception.Message, Does.Contain(PluginName));
            Assert.That(exception.Message, Does.Contain("requests>=2"));
            Assert.That(uvLocateCount, Is.EqualTo(0));
            Assert.That(runner.Calls, Is.Empty);
        }

        [Test]
        public async Task UnpinnedAndAllowed_Installs()
        {
            settings.AllowUnpinnedPythonDependencies = true;
            var installer = CreateInstaller();

            var result = await installer.EnsureInstalledAsync(CreateMetadata("requests>=2"), pythonPath, CancellationToken.None);

            Assert.That(result, Is.EqualTo(SitePackagesFor(KeyFor("requests>=2"))));
            Assert.That(Directory.Exists(result), Is.True);
            Assert.That(runner.InstallCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task FirstInstall_RunsUvWithExpectedArguments()
        {
            var installer = CreateInstaller();

            var result = await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);

            var key = KeyFor("a==1");
            var expectedTarget = SitePackagesFor(key);
            Assert.That(result, Is.EqualTo(expectedTarget));
            Assert.That(expectedTarget, Does.Contain("deps root"));

            var install = runner.InstallCalls.Single();
            Assert.That(install.FileName, Is.EqualTo("uv"));
            Assert.That(install.Arguments, Is.EqualTo(new[]
            {
                "pip", "install", "--no-config", "--target", expectedTarget, "--python", pythonPath, "a==1"
            }));
            Assert.That(install.Environment["UV_NO_CONFIG"], Is.EqualTo("1"));
            Assert.That(install.Environment.ContainsKey("UV_CACHE_DIR"), Is.False);

            var marker = DependencyInstallMarker.Load(MarkerPath);
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.Matches(key), Is.True);
        }

        [Test]
        public async Task Portable_SetsUvCacheDir()
        {
            var installer = CreateInstaller(portable: true);

            await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);

            var install = runner.InstallCalls.Single();
            Assert.That(install.Environment["UV_CACHE_DIR"], Is.EqualTo(cachePath));
            Assert.That(install.Environment["UV_NO_CONFIG"], Is.EqualTo("1"));
        }

        [Test]
        public async Task MarkerMatches_NoUvLocateNoInstall()
        {
            var locator = CreateLocator();
            var first = CreateInstaller(locator: locator);
            var expected = await first.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);
            Assert.That(uvLocateCount, Is.EqualTo(1));

            var secondRunner = new FakeProcessRunner();
            var second = CreateInstaller(secondRunner, locator);
            var result = await second.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);

            Assert.That(result, Is.EqualTo(expected));
            Assert.That(uvLocateCount, Is.EqualTo(1));
            Assert.That(runner.InstallCalls, Has.Count.EqualTo(1));
            Assert.That(secondRunner.InstallCalls, Is.Empty);
            Assert.That(secondRunner.Calls.Where(call => !call.IsPythonProbe), Is.Empty);
        }

        [Test]
        public async Task DependenciesChanged_InstallsIntoNewKey_KeepsOldUntilCleanup()
        {
            var installer = CreateInstaller();
            var oldPath = await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);

            var newPath = await installer.EnsureInstalledAsync(CreateMetadata("a==2"), pythonPath, CancellationToken.None);

            Assert.That(newPath, Is.Not.EqualTo(oldPath));
            Assert.That(Directory.Exists(newPath), Is.True);
            Assert.That(Directory.Exists(oldPath), Is.True);
            Assert.That(DependencyInstallMarker.Load(MarkerPath).Matches(KeyFor("a==2")), Is.True);
            Assert.That(runner.InstallCalls, Has.Count.EqualTo(2));

            installer.CleanupStaleInstalls(PluginId);

            Assert.That(Directory.Exists(Path.GetDirectoryName(oldPath)), Is.False);
            Assert.That(Directory.Exists(newPath), Is.True);
        }

        [Test]
        public void CleanupStaleInstalls_NoValidMarker_DeletesNothing()
        {
            var staleDirectory = Path.Combine(dependenciesRoot, PluginId, "0123456789abcdef", "site-packages");
            Directory.CreateDirectory(staleDirectory);
            var installer = CreateInstaller();

            installer.CleanupStaleInstalls(PluginId);
            Assert.That(Directory.Exists(staleDirectory), Is.True);

            File.WriteAllText(MarkerPath, "{not json");
            installer.CleanupStaleInstalls(PluginId);
            Assert.That(Directory.Exists(staleDirectory), Is.True);
        }

        [Test]
        public async Task MarkerMatches_DoesNotCleanUpStaleInstalls()
        {
            var installer = CreateInstaller();
            var path = await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);
            var staleDirectory = Path.Combine(dependenciesRoot, PluginId, "0123456789abcdef");
            Directory.CreateDirectory(staleDirectory);

            var result = await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);

            Assert.That(result, Is.EqualTo(path));
            Assert.That(Directory.Exists(staleDirectory), Is.True);
        }

        [Test]
        public async Task InstallFails_KeepsOldMarkerAndFolder_Throws()
        {
            var installer = CreateInstaller();
            var oldPath = await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);
            runner.InstallStdErr = "Resolving dependencies...\n\nerror: No solution found\nversion a==2 does not exist\n";
            runner.InstallExitCode = 1;

            var exception = Assert.ThrowsAsync<PythonDependencyException>(() =>
                installer.EnsureInstalledAsync(CreateMetadata("a==2"), pythonPath, CancellationToken.None));

            Assert.That(exception.Message, Does.Contain(PluginName));
            Assert.That(exception.Message, Does.Contain("version a==2 does not exist"));
            Assert.That(Directory.Exists(oldPath), Is.True);
            Assert.That(DependencyInstallMarker.Load(MarkerPath).Matches(KeyFor("a==1")), Is.True);
            Assert.That(logged, Has.Some.Contain("No solution found"));
        }

        [Test]
        public void InstallFails_OnlyLastFiveNonBlankLinesInMessage()
        {
            runner.InstallExitCode = 1;
            runner.InstallStdErr = "line1\nline2\n\nline3\nline4\nline5\nline6\nline7\n";
            var installer = CreateInstaller();

            var exception = Assert.ThrowsAsync<PythonDependencyException>(() =>
                installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None));

            Assert.That(exception.Message, Does.Not.Contain("line1"));
            Assert.That(exception.Message, Does.Not.Contain("line2"));
            Assert.That(exception.Message, Does.Contain("line3"));
            Assert.That(exception.Message, Does.Contain("line7"));
        }

        [Test]
        public void UvLocateFails_ThrowsPythonDependencyException()
        {
            var locator = new UvLocator(
                settings,
                Path.Combine(tempDirectory, "uv"),
                _ => false,
                () => null,
                (_, _) => throw new InvalidOperationException("offline"));
            var installer = CreateInstaller(locator: locator);

            var exception = Assert.ThrowsAsync<PythonDependencyException>(() =>
                installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None));

            Assert.That(exception.Message, Does.Contain(PluginName));
            Assert.That(exception.InnerException, Is.Not.Null);
            Assert.That(runner.InstallCalls, Is.Empty);
        }

        [Test]
        public void UvDownloadTimesOut_TokenNotCancelled_ThrowsPythonDependencyException()
        {
            var locator = new UvLocator(
                settings,
                Path.Combine(tempDirectory, "uv"),
                _ => false,
                () => null,
                (_, _) => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"));
            var installer = CreateInstaller(locator: locator);

            var exception = Assert.ThrowsAsync<PythonDependencyException>(() =>
                installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None));

            Assert.That(exception.Message, Does.Contain(PluginName));
            Assert.That(exception.InnerException, Is.InstanceOf<TaskCanceledException>());
        }

        [Test]
        public void UvDownloadCancelled_TokenCancelled_PropagatesOperationCanceledException()
        {
            using var source = new CancellationTokenSource();
            var locator = new UvLocator(
                settings,
                Path.Combine(tempDirectory, "uv"),
                _ => false,
                () => null,
                (_, token) =>
                {
                    source.Cancel();
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult("uv");
                });
            var installer = CreateInstaller(locator: locator);

            Assert.CatchAsync<OperationCanceledException>(() =>
                installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, source.Token));
        }

        [Test]
        public void ProcessTimesOut_TokenNotCancelled_ThrowsPythonDependencyException()
        {
            runner.InstallException = new TaskCanceledException("timeout");
            var installer = CreateInstaller();

            var exception = Assert.ThrowsAsync<PythonDependencyException>(() =>
                installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None));

            Assert.That(exception.Message, Does.Contain(PluginName));
        }

        [Test]
        public void PythonProbeFails_ThrowsPythonDependencyException()
        {
            runner.ProbeExitCode = 1;
            runner.ProbeStdErr = "python is broken";
            var installer = CreateInstaller();

            var exception = Assert.ThrowsAsync<PythonDependencyException>(() =>
                installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None));

            Assert.That(exception.Message, Does.Contain(PluginName));
            Assert.That(uvLocateCount, Is.EqualTo(0));
        }

        [Test]
        public async Task ConcurrentSamePlugin_InstallsOnce()
        {
            runner.InstallDelay = TimeSpan.FromMilliseconds(150);
            var installer = CreateInstaller();

            var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
                Task.Run(() => installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None))));

            Assert.That(results.Distinct().Count(), Is.EqualTo(1));
            Assert.That(runner.InstallCalls, Has.Count.EqualTo(1));
            Assert.That(uvLocateCount, Is.EqualTo(1));
        }

        [Test]
        public async Task CorruptMarker_Reinstalls()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath));
            File.WriteAllText(MarkerPath, "{not json");
            var installer = CreateInstaller();

            var result = await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);

            Assert.That(Directory.Exists(result), Is.True);
            Assert.That(runner.InstallCalls, Has.Count.EqualTo(1));
            Assert.That(DependencyInstallMarker.Load(MarkerPath).Matches(KeyFor("a==1")), Is.True);
        }

        [Test]
        public async Task PythonVersionProbe_RunsOncePerPythonPath()
        {
            var installer = CreateInstaller();

            await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);
            await installer.EnsureInstalledAsync(CreateMetadata("a==2"), pythonPath, CancellationToken.None);

            Assert.That(runner.Calls.Count(call => call.IsPythonProbe), Is.EqualTo(1));
        }

        [Test]
        public void ToConsolePython_PrefersSiblingPythonExe()
        {
            var directory = Path.Combine(tempDirectory, "console");
            Directory.CreateDirectory(directory);
            var pythonw = Path.Combine(directory, "pythonw.exe");
            var python = Path.Combine(directory, "python.exe");
            File.WriteAllText(pythonw, string.Empty);

            Assert.That(PythonDependencyInstaller.ToConsolePython(pythonw), Is.EqualTo(pythonw));

            File.WriteAllText(python, string.Empty);

            Assert.That(PythonDependencyInstaller.ToConsolePython(pythonw), Is.EqualTo(python));
            Assert.That(PythonDependencyInstaller.ToConsolePython(python), Is.EqualTo(python));
        }

        [Test]
        public async Task RemovePlugin_DeletesPluginRoot()
        {
            var installer = CreateInstaller();
            await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);
            Assert.That(Directory.Exists(Path.Combine(dependenciesRoot, PluginId)), Is.True);

            installer.RemovePlugin(PluginId);

            Assert.That(Directory.Exists(Path.Combine(dependenciesRoot, PluginId)), Is.False);
        }

        [Test]
        public void RemovePlugin_MissingPlugin_DoesNotThrow()
        {
            var installer = CreateInstaller();

            Assert.DoesNotThrow(() => installer.RemovePlugin("never-installed"));
        }

        private sealed record Call(
            string FileName,
            IReadOnlyList<string> Arguments,
            IReadOnlyDictionary<string, string> Environment)
        {
            public bool IsPythonProbe => Arguments.Count > 0 && Arguments[0] == "-c";
        }

        private sealed class FakeProcessRunner : IProcessRunner
        {
            private readonly object gate = new();
            private readonly List<Call> calls = new();

            public int ProbeExitCode { get; set; }
            public string ProbeStdErr { get; set; } = string.Empty;
            public int InstallExitCode { get; set; }
            public string InstallStdErr { get; set; } = string.Empty;
            public TimeSpan InstallDelay { get; set; } = TimeSpan.Zero;
            public Exception InstallException { get; set; }

            public IReadOnlyList<Call> Calls
            {
                get
                {
                    lock (gate)
                    {
                        return calls.ToList();
                    }
                }
            }

            public IReadOnlyList<Call> InstallCalls =>
                Calls.Where(call => call.Arguments.Count > 1 && call.Arguments[0] == "pip" && call.Arguments[1] == "install").ToList();

            public async Task<ProcessResult> RunAsync(
                string fileName,
                IReadOnlyList<string> arguments,
                IReadOnlyDictionary<string, string> environment,
                CancellationToken token)
            {
                var call = new Call(fileName, arguments.ToList(), new Dictionary<string, string>(environment));
                lock (gate)
                {
                    calls.Add(call);
                }

                if (call.IsPythonProbe)
                {
                    return ProbeExitCode == 0
                        ? new ProcessResult(0, PythonVersionProbeOutput + "\r\n", string.Empty)
                        : new ProcessResult(ProbeExitCode, string.Empty, ProbeStdErr);
                }

                if (arguments.Count > 1 && arguments[0] == "pip" && arguments[1] == "install")
                {
                    if (InstallDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(InstallDelay, token);
                    }

                    if (InstallException is not null)
                    {
                        throw InstallException;
                    }

                    if (InstallExitCode != 0)
                    {
                        return new ProcessResult(InstallExitCode, "installing", InstallStdErr);
                    }

                    var targetIndex = arguments.ToList().IndexOf("--target");
                    Directory.CreateDirectory(arguments[targetIndex + 1]);
                    return new ProcessResult(0, string.Empty, string.Empty);
                }

                if (arguments.Count > 1 && arguments[0] == "pip" && arguments[1] == "freeze")
                {
                    return new ProcessResult(0, "a==1\r\n\r\n", string.Empty);
                }

                if (arguments.Count == 1 && arguments[0] == "--version")
                {
                    return new ProcessResult(0, "uv 0.13.7 (abc123 2026-11-04)\r\n", string.Empty);
                }

                throw new InvalidOperationException("Unexpected process call: " + string.Join(" ", arguments));
            }
        }
    }
}
