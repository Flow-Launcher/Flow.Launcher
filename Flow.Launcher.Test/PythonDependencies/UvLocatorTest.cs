using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Core.ExternalPlugins.PythonDependencies;
using Flow.Launcher.Infrastructure.UserSettings;
using NUnit.Framework;

namespace Flow.Launcher.Test.PythonDependencies
{
    [TestFixture]
    public class UvLocatorTest
    {
        private string managedRoot;
        private PluginsSettings settings;

        [SetUp]
        public void SetUp()
        {
            managedRoot = Path.Combine(Path.GetTempPath(), "FlowUvLocatorTest_" + Guid.NewGuid().ToString("N"));
            settings = new PluginsSettings();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(managedRoot))
            {
                Directory.Delete(managedRoot, recursive: true);
            }
        }

        private string ManagedUv(string version) => Path.Combine(managedRoot, version, "uv.exe");

        [Test]
        public async Task ConfiguredPathExists_ReturnsItWithoutPathSearchOrDownload()
        {
            var configured = Path.Combine(Path.GetTempPath(), "custom", "uv.exe");
            settings.UvExecutablePath = configured;
            var pathSearches = 0;
            var downloads = 0;
            var locator = new UvLocator(settings, managedRoot,
                path => path == configured,
                () => { pathSearches++; return "on-path"; },
                (dir, token) => { downloads++; return Task.FromResult("downloaded"); });

            var result = await locator.GetUvPathAsync(CancellationToken.None);

            Assert.That(result, Is.EqualTo(configured));
            Assert.That(pathSearches, Is.EqualTo(0));
            Assert.That(downloads, Is.EqualTo(0));
        }

        [Test]
        public async Task NoConfiguredPath_UsesPathAndDoesNotSaveSetting()
        {
            var downloads = 0;
            var locator = new UvLocator(settings, managedRoot,
                path => false,
                () => "on-path/uv.exe",
                (dir, token) => { downloads++; return Task.FromResult("downloaded"); });

            var result = await locator.GetUvPathAsync(CancellationToken.None);

            Assert.That(result, Is.EqualTo("on-path/uv.exe"));
            Assert.That(settings.UvExecutablePath, Is.Empty);
            Assert.That(downloads, Is.EqualTo(0));
        }

        [Test]
        public async Task NothingFound_DownloadsIntoVersionFolderAndSavesSetting()
        {
            string downloadDir = null;
            var locator = new UvLocator(settings, managedRoot,
                path => false,
                () => null,
                (dir, token) =>
                {
                    downloadDir = dir;
                    return Task.FromResult(Path.Combine(dir, "uv.exe"));
                });

            var result = await locator.GetUvPathAsync(CancellationToken.None);

            Assert.That(downloadDir, Does.EndWith("0.13.7"));
            Assert.That(Path.GetDirectoryName(downloadDir), Is.EqualTo(managedRoot));
            Assert.That(settings.UvExecutablePath, Is.EqualTo(result));
        }

        [Test]
        public async Task ConfiguredManagedOldVersion_Redownloads()
        {
            var oldUv = ManagedUv("0.1.0");
            Directory.CreateDirectory(Path.GetDirectoryName(oldUv));
            File.WriteAllText(oldUv, "old");
            settings.UvExecutablePath = oldUv;
            var downloads = 0;
            var locator = new UvLocator(settings, managedRoot,
                File.Exists,
                () => null,
                (dir, token) =>
                {
                    downloads++;
                    return Task.FromResult(Path.Combine(dir, "uv.exe"));
                });

            var result = await locator.GetUvPathAsync(CancellationToken.None);

            Assert.That(downloads, Is.EqualTo(1));
            Assert.That(result, Is.EqualTo(ManagedUv("0.13.7")));
            Assert.That(Directory.Exists(Path.GetDirectoryName(oldUv)), Is.False);
        }

        [Test]
        public async Task ConfiguredManagedCurrentVersion_IsKept()
        {
            var currentUv = ManagedUv("0.13.7");
            Directory.CreateDirectory(Path.GetDirectoryName(currentUv));
            File.WriteAllText(currentUv, "current");
            settings.UvExecutablePath = currentUv;
            var downloads = 0;
            var locator = new UvLocator(settings, managedRoot,
                File.Exists,
                () => null,
                (dir, token) => { downloads++; return Task.FromResult("downloaded"); });

            var result = await locator.GetUvPathAsync(CancellationToken.None);

            Assert.That(result, Is.EqualTo(currentUv));
            Assert.That(downloads, Is.EqualTo(0));
        }

        [Test]
        public async Task ConcurrentCalls_DownloadOnce()
        {
            var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var downloads = 0;
            var locator = new UvLocator(settings, managedRoot,
                path => false,
                () => null,
                (dir, token) =>
                {
                    Interlocked.Increment(ref downloads);
                    return gate.Task;
                });

            var calls = Enumerable.Range(0, 10)
                .Select(_ => Task.Run(() => locator.GetUvPathAsync(CancellationToken.None)))
                .ToArray();
            await Task.Delay(100);
            gate.SetResult("downloaded/uv.exe");
            var results = await Task.WhenAll(calls);

            Assert.That(downloads, Is.EqualTo(1));
            Assert.That(results, Is.All.EqualTo("downloaded/uv.exe"));
        }

        [Test]
        public async Task DownloadFails_NextCallRetries()
        {
            var downloads = 0;
            var locator = new UvLocator(settings, managedRoot,
                path => false,
                () => null,
                (dir, token) =>
                {
                    downloads++;
                    if (downloads == 1)
                    {
                        throw new InvalidOperationException("offline");
                    }
                    return Task.FromResult(Path.Combine(dir, "uv.exe"));
                });

            Assert.ThrowsAsync<InvalidOperationException>(() => locator.GetUvPathAsync(CancellationToken.None));
            var result = await locator.GetUvPathAsync(CancellationToken.None);

            Assert.That(downloads, Is.EqualTo(2));
            Assert.That(result, Does.EndWith("uv.exe"));
        }

        [Test]
        public async Task ConfiguredManagedOldVersion_DownloadFails_KeepsAndUsesOldUv()
        {
            var oldUv = ManagedUv("0.1.0");
            Directory.CreateDirectory(Path.GetDirectoryName(oldUv));
            File.WriteAllText(oldUv, "old");
            settings.UvExecutablePath = oldUv;
            var locator = new UvLocator(settings, managedRoot,
                File.Exists,
                () => null,
                (_, _) => throw new InvalidOperationException("offline"));

            var result = await locator.GetUvPathAsync(CancellationToken.None);

            Assert.That(result, Is.EqualTo(oldUv));
            Assert.That(File.Exists(oldUv), Is.True);
            Assert.That(settings.UvExecutablePath, Is.EqualTo(oldUv));
        }

        [Test]
        public async Task ConfiguredManagedOldVersion_UvOnPath_RemovesOldUv()
        {
            var oldUv = ManagedUv("0.1.0");
            Directory.CreateDirectory(Path.GetDirectoryName(oldUv));
            File.WriteAllText(oldUv, "old");
            settings.UvExecutablePath = oldUv;
            var locator = new UvLocator(settings, managedRoot,
                File.Exists,
                () => "path/uv.exe",
                (_, _) => throw new InvalidOperationException("download should not be used"));

            var result = await locator.GetUvPathAsync(CancellationToken.None);

            Assert.That(result, Is.EqualTo("path/uv.exe"));
            Assert.That(Directory.Exists(Path.GetDirectoryName(oldUv)), Is.False);
        }

        [Test]
        public async Task SettingChanged_LocatesAgain()
        {
            var chosen = Path.Combine(Path.GetTempPath(), "FlowUvChosen_" + Guid.NewGuid().ToString("N"), "uv.exe");
            var pathLookups = 0;
            var locator = new UvLocator(settings, managedRoot,
                path => path == chosen,
                () =>
                {
                    pathLookups++;
                    return "path/uv.exe";
                },
                (_, _) => throw new InvalidOperationException("download should not be used"));

            var first = await locator.GetUvPathAsync(CancellationToken.None);
            var cached = await locator.GetUvPathAsync(CancellationToken.None);
            settings.UvExecutablePath = chosen;
            var changed = await locator.GetUvPathAsync(CancellationToken.None);

            Assert.That(first, Is.EqualTo("path/uv.exe"));
            Assert.That(cached, Is.EqualTo("path/uv.exe"));
            Assert.That(pathLookups, Is.EqualTo(1));
            Assert.That(changed, Is.EqualTo(chosen));
        }
    }
}
