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
        private const string PythonVersionProbeOutput = "3.11.9 win32";

        private string tempDirectory;
        private string dependenciesRoot;
        private string cachePath;
        private string pythonPath;
        private PluginsSettings settings;
        private FakeProcessRunner runner;
        private int uvLocateCount;
        private List<string> logged;
        private List<string> installingNotices;

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
            installingNotices = new List<string>();
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
                path => path == "uv",
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
            bool portable = false,
            Action<PluginMetadata> onInstalling = null,
            TimeSpan? processTimeout = null)
        {
            return new PythonDependencyInstaller(
                settings,
                locator ?? CreateLocator(),
                processRunner ?? runner,
                dependenciesRoot,
                portable,
                cachePath,
                logged.Add,
                onInstalling ?? (metadata => installingNotices.Add(metadata.Name)),
                processTimeout);
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

        private string PluginRoot => Path.Combine(dependenciesRoot, PluginId);

        private string MarkerPath => Path.Combine(PluginRoot, DependencyInstallMarker.FileName);

        private void AssertInstalledFor(string sitePackages, string key)
        {
            var installFolder = Path.GetFileName(Path.GetDirectoryName(sitePackages));
            Assert.That(Path.GetFileName(sitePackages), Is.EqualTo("site-packages"));
            Assert.That(Path.GetDirectoryName(Path.GetDirectoryName(sitePackages)), Is.EqualTo(PluginRoot));
            Assert.That(installFolder, Does.Match("^" + key + "-[0-9a-f]{8}$"));
            Assert.That(DependencyInstallMarker.Load(MarkerPath)?.InstallFolder, Is.EqualTo(installFolder));
        }

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

            AssertInstalledFor(result, KeyFor("requests>=2"));
            Assert.That(Directory.Exists(result), Is.True);
            Assert.That(runner.InstallCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task FirstInstall_RunsUvWithExpectedArguments()
        {
            var installer = CreateInstaller();

            var result = await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);

            var key = KeyFor("a==1");
            AssertInstalledFor(result, key);
            Assert.That(result, Does.Contain("deps root"));

            var install = runner.InstallCalls.Single();
            Assert.That(install.FileName, Is.EqualTo("uv"));
            Assert.That(install.Arguments, Is.EqualTo(new[]
            {
                "pip", "install", "--no-config", "--target", result, "--python", pythonPath, "a==1"
            }));
            Assert.That(install.Environment["UV_NO_CONFIG"], Is.EqualTo("1"));
            Assert.That(install.Environment.ContainsKey("UV_CACHE_DIR"), Is.False);

            var probe = runner.Calls.Single(call => call.IsPythonProbe);
            Assert.That(probe.FileName, Is.EqualTo(pythonPath));
            Assert.That(probe.Arguments[1], Does.Contain("sysconfig.get_platform()"));

            var marker = DependencyInstallMarker.Load(MarkerPath);
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.Matches(key), Is.True);
        }

        [Test]
        public async Task FirstInstall_NotifiesInstallingOnce_MarkerMatchDoesNot()
        {
            var installer = CreateInstaller();

            await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);
            Assert.That(installingNotices, Is.EqualTo(new[] { PluginName }));

            await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);
            await CreateInstaller(new FakeProcessRunner()).EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);
            Assert.That(installingNotices, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task DisabledPlugin_InstallsWithoutNotice()
        {
            var metadata = CreateMetadata("a==1");
            metadata.Disabled = true;
            metadata.HomeDisabled = true;
            var installer = CreateInstaller();

            var result = await installer.EnsureInstalledAsync(metadata, pythonPath, CancellationToken.None);

            AssertInstalledFor(result, KeyFor("a==1"));
            Assert.That(installingNotices, Is.Empty);
        }

        [Test]
        public void ValidationFails_DoesNotNotifyInstalling()
        {
            var installer = CreateInstaller();
            var invalidId = CreateMetadata("a==1");
            invalidId.ID = "..";

            Assert.ThrowsAsync<PythonDependencyException>(() =>
                installer.EnsureInstalledAsync(CreateMetadata("requests>=2"), pythonPath, CancellationToken.None));
            Assert.ThrowsAsync<PythonDependencyException>(() =>
                installer.EnsureInstalledAsync(invalidId, pythonPath, CancellationToken.None));

            Assert.That(installingNotices, Is.Empty);
        }

        [Test]
        public async Task InstallingNoticeThrows_InstallStillSucceeds()
        {
            var installer = CreateInstaller(onInstalling: _ => throw new InvalidOperationException("no window yet"));

            var result = await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);

            AssertInstalledFor(result, KeyFor("a==1"));
            Assert.That(logged, Has.Some.Contain("no window yet"));
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
        public async Task MarkerMatches_InstallFolderMissing_Reinstalls()
        {
            var installer = CreateInstaller();
            var first = await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);
            Directory.Delete(Path.GetDirectoryName(first), recursive: true);

            var result = await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);

            Assert.That(result, Is.Not.EqualTo(first));
            Assert.That(Directory.Exists(result), Is.True);
            AssertInstalledFor(result, KeyFor("a==1"));
            Assert.That(runner.InstallCalls, Has.Count.EqualTo(2));
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

            await installer.CleanupStaleInstallsAsync(PluginId);

            Assert.That(Directory.Exists(Path.GetDirectoryName(oldPath)), Is.False);
            Assert.That(Directory.Exists(newPath), Is.True);
        }

        [Test]
        public async Task FolderOfSameKeyLeftByStrayWriter_NeverDeletedOrReused()
        {
            var key = KeyFor("a==1");
            var strayFolders = new[] { Path.Combine(PluginRoot, key), Path.Combine(PluginRoot, key + "-0badf00d") };
            foreach (var folder in strayFolders)
            {
                Directory.CreateDirectory(Path.Combine(folder, "site-packages"));
                File.WriteAllText(Path.Combine(folder, "site-packages", "partial.py"), "still being written");
            }

            var installer = CreateInstaller();
            var result = await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);

            AssertInstalledFor(result, key);
            Assert.That(strayFolders, Does.Not.Contain(Path.GetDirectoryName(result)));
            foreach (var folder in strayFolders)
            {
                Assert.That(File.Exists(Path.Combine(folder, "site-packages", "partial.py")), Is.True);
            }

            await installer.CleanupStaleInstallsAsync(PluginId);

            Assert.That(strayFolders.Where(Directory.Exists), Is.Empty);
            Assert.That(Directory.Exists(result), Is.True);
        }

        [Test]
        public async Task OldMarkerWithoutInstallFolder_Reinstalls()
        {
            var key = KeyFor("a==1");
            Directory.CreateDirectory(Path.Combine(PluginRoot, key, "site-packages"));
            File.WriteAllText(MarkerPath, $$"""{"InstallKey":"{{key}}","Dependencies":["a==1"]}""");
            var installer = CreateInstaller();

            var result = await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);

            AssertInstalledFor(result, key);
            Assert.That(runner.InstallCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task CleanupStaleInstalls_NoValidMarker_DeletesNothing()
        {
            var staleDirectory = Path.Combine(dependenciesRoot, PluginId, "0123456789abcdef", "site-packages");
            Directory.CreateDirectory(staleDirectory);
            var installer = CreateInstaller();

            await installer.CleanupStaleInstallsAsync(PluginId);
            Assert.That(Directory.Exists(staleDirectory), Is.True);

            File.WriteAllText(MarkerPath, "{not json");
            await installer.CleanupStaleInstallsAsync(PluginId);
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
            Assert.That(Directory.EnumerateDirectories(PluginRoot), Is.EqualTo(new[] { Path.GetDirectoryName(oldPath) }));
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
        public void UvHangs_TimesOut_ThrowsPythonDependencyException()
        {
            runner.InstallDelay = TimeSpan.FromMinutes(5);
            var installer = CreateInstaller(processTimeout: TimeSpan.FromMilliseconds(50));

            var exception = Assert.ThrowsAsync<PythonDependencyException>(() =>
                installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None));

            Assert.That(exception.Message, Does.Contain(PluginName));
            Assert.That(exception.InnerException, Is.InstanceOf<OperationCanceledException>());
            Assert.That(logged, Has.Some.Contain("did not finish"));
            Assert.That(Directory.Exists(PluginRoot) ? Directory.EnumerateDirectories(PluginRoot) : Array.Empty<string>(), Is.Empty);
        }

        [Test]
        public void CallerCancelsDuringInstall_PropagatesOperationCanceledException()
        {
            runner.InstallDelay = TimeSpan.FromMinutes(5);
            using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            var installer = CreateInstaller();

            Assert.CatchAsync<OperationCanceledException>(() =>
                installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, source.Token));
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
            var otherPython = Path.Combine(tempDirectory, "other py", "python.exe");
            var installer = CreateInstaller();

            await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);
            await installer.EnsureInstalledAsync(CreateMetadata("a==2"), pythonPath, CancellationToken.None);
            await installer.EnsureInstalledAsync(CreateMetadata("a==3"), otherPython, CancellationToken.None);
            await installer.EnsureInstalledAsync(CreateMetadata("a==4"), otherPython, CancellationToken.None);

            var probedPythons = runner.Calls.Where(call => call.IsPythonProbe).Select(call => call.FileName).ToList();
            Assert.That(probedPythons, Is.EqualTo(new[] { pythonPath, otherPython }));
        }

        [Test]
        public async Task PythonVersionProbe_PythonReplacedInPlace_ProbesAgain()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(pythonPath));
            File.WriteAllText(pythonPath, "old");
            File.SetLastWriteTimeUtc(pythonPath, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var installer = CreateInstaller();
            await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);

            File.SetLastWriteTimeUtc(pythonPath, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            await installer.EnsureInstalledAsync(CreateMetadata("a==2"), pythonPath, CancellationToken.None);

            Assert.That(runner.Calls.Count(call => call.IsPythonProbe), Is.EqualTo(2));
        }

        [Test]
        public async Task PythonVersionProbe_IncludesImplementationAndAbi()
        {
            var installer = CreateInstaller();

            await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);

            var probe = runner.Calls.Single(call => call.IsPythonProbe).Arguments[1];
            Assert.That(probe, Does.Contain("sys.implementation.name").And.Contain("EXT_SUFFIX").And.Contain("sysconfig.get_platform()"));
        }

        [Test]
        public async Task CleanupStaleInstalls_WaitsForRunningInstall()
        {
            var installer = CreateInstaller();
            var old = await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);
            runner.InstallDelay = TimeSpan.FromMilliseconds(300);

            var install = installer.EnsureInstalledAsync(CreateMetadata("a==2"), pythonPath, CancellationToken.None);
            await WaitUntilAsync(() => Directory.GetDirectories(PluginRoot).Length == 2);
            await installer.CleanupStaleInstallsAsync(PluginId);
            var result = await install;

            Assert.That(Directory.Exists(result), Is.True);
            AssertInstalledFor(result, KeyFor("a==2"));
            Assert.That(Directory.Exists(Path.GetDirectoryName(old)), Is.False);
        }

        [Test]
        public async Task DeleteDependencies_CancelsRunningInstallAndDeletesRoot()
        {
            runner.InstallDelay = TimeSpan.FromMinutes(5);
            var installer = CreateInstaller();

            var install = installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);
            await WaitUntilAsync(() => runner.InstallCalls.Count == 1);
            var delete = installer.DeleteDependenciesAsync(PluginId);

            Assert.That(await Task.WhenAny(delete, Task.Delay(TimeSpan.FromSeconds(10))), Is.SameAs(delete));
            Assert.That(async () => await install, Throws.InstanceOf<OperationCanceledException>());
            Assert.That(Directory.Exists(PluginRoot), Is.False);
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                {
                    Assert.Fail("Timed out waiting for the condition");
                }

                await Task.Delay(10);
            }
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
        public async Task DeleteDependencies_DeletesPluginRoot()
        {
            var installer = CreateInstaller();
            await installer.EnsureInstalledAsync(CreateMetadata("a==1"), pythonPath, CancellationToken.None);
            Assert.That(Directory.Exists(PluginRoot), Is.True);

            await installer.DeleteDependenciesAsync(PluginId);

            Assert.That(Directory.Exists(PluginRoot), Is.False);
        }

        private static readonly string[] UnusablePluginIds =
        {
            null, "", " ", ".", "..", "abc.", " abc", "abc ", "a/b", "a\\b", "../other", "..\\other", "/rooted", "C:\\rooted", "a\0b"
        };

        private void PlantDeletionTargets()
        {
            foreach (var folder in new[] { tempDirectory, dependenciesRoot, Path.Combine(dependenciesRoot, "a") })
            {
                Directory.CreateDirectory(folder);
                new DependencyInstallMarker("0123456789abcdef", new[] { "a==1" }, pythonPath, PythonVersionProbeOutput, "uv", new[] { "a==1" }, "0123456789abcdef-00000000")
                    .Save(Path.Combine(folder, DependencyInstallMarker.FileName));
            }

            Directory.CreateDirectory(Path.Combine(dependenciesRoot, "other-plugin", "0123456789abcdef-11111111", "site-packages"));
            Directory.CreateDirectory(Path.Combine(dependenciesRoot, "a", "b", "site-packages"));
            Directory.CreateDirectory(Path.Combine(tempDirectory, "other", "site-packages"));
        }

        private string[] SandboxEntries() =>
            Directory.EnumerateFileSystemEntries(tempDirectory, "*", SearchOption.AllDirectories).Order().ToArray();

        [TestCaseSource(nameof(UnusablePluginIds))]
        public void EnsureInstalled_UnusablePluginId_ThrowsBeforeTouchingDisk(string pluginId)
        {
            PlantDeletionTargets();
            var before = SandboxEntries();
            var metadata = CreateMetadata("a==1");
            metadata.ID = pluginId;
            var installer = CreateInstaller();

            var exception = Assert.ThrowsAsync<PythonDependencyException>(() =>
                installer.EnsureInstalledAsync(metadata, pythonPath, CancellationToken.None));

            Assert.That(exception.Message, Does.Contain(PluginName));
            Assert.That(runner.Calls, Is.Empty);
            Assert.That(uvLocateCount, Is.EqualTo(0));
            Assert.That(SandboxEntries(), Is.EqualTo(before));
        }

        [TestCaseSource(nameof(UnusablePluginIds))]
        public async Task CleanupStaleInstalls_UnusablePluginId_DeletesNothing(string pluginId)
        {
            PlantDeletionTargets();
            var before = SandboxEntries();
            var installer = CreateInstaller();

            await installer.CleanupStaleInstallsAsync(pluginId);

            Assert.That(SandboxEntries(), Is.EqualTo(before));
            Assert.That(logged, Is.Not.Empty);
        }

        [TestCaseSource(nameof(UnusablePluginIds))]
        public async Task DeleteDependencies_UnusablePluginId_DeletesNothing(string pluginId)
        {
            PlantDeletionTargets();
            var before = SandboxEntries();
            var installer = CreateInstaller();

            await installer.DeleteDependenciesAsync(pluginId);

            Assert.That(SandboxEntries(), Is.EqualTo(before));
            Assert.That(logged, Is.Not.Empty);
        }

        [Test]
        public void RemoveOrphanedDependencies_DeletesOnlyFoldersOfUninstalledPlugins()
        {
            var kept = Path.Combine(dependenciesRoot, "Installed-Plugin", "key-00000000", "site-packages");
            var orphan = Path.Combine(dependenciesRoot, "uninstalled-plugin", "key-00000000", "site-packages");
            Directory.CreateDirectory(kept);
            Directory.CreateDirectory(orphan);
            var installer = CreateInstaller();

            installer.RemoveOrphanedDependencies(new[] { "installed-plugin" });

            Assert.That(Directory.Exists(kept), Is.True);
            Assert.That(Directory.Exists(Path.Combine(dependenciesRoot, "uninstalled-plugin")), Is.False);
        }

        [Test]
        public void RemoveOrphanedDependencies_NoInstalledPlugins_DeletesNothing()
        {
            var orphan = Path.Combine(dependenciesRoot, "some-plugin", "site-packages");
            Directory.CreateDirectory(orphan);
            var installer = CreateInstaller();

            installer.RemoveOrphanedDependencies(Array.Empty<string>());

            Assert.That(Directory.Exists(orphan), Is.True);
        }

        [Test]
        public void RemoveOrphanedDependencies_NoDependenciesRoot_DoesNotThrow()
        {
            var installer = CreateInstaller();

            Assert.DoesNotThrow(() => installer.RemoveOrphanedDependencies(new[] { PluginId }));
        }

        [Test]
        public void DeleteDependencies_MissingPlugin_DoesNotThrow()
        {
            var installer = CreateInstaller();

            Assert.DoesNotThrowAsync(() => installer.DeleteDependenciesAsync("never-installed"));
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
                    // uv creates the target as soon as it starts, so a running install already has a folder on disk.
                    var targetIndex = arguments.ToList().IndexOf("--target");
                    Directory.CreateDirectory(arguments[targetIndex + 1]);

                    if (InstallDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(InstallDelay, token);
                    }

                    if (InstallException is not null)
                    {
                        throw InstallException;
                    }

                    return InstallExitCode != 0
                        ? new ProcessResult(InstallExitCode, "installing", InstallStdErr)
                        : new ProcessResult(0, string.Empty, string.Empty);
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
