using System;
using System.IO;
using System.Text.Json;
using Flow.Launcher.Core.ExternalPlugins.PythonDependencies;
using NUnit.Framework;

namespace Flow.Launcher.Test.PythonDependencies
{
    [TestFixture]
    public class DependencyInstallMarkerTest
    {
        private static readonly string[] Dependencies = { "requests==2.32.3", "rich==13.7.1" };
        private const string PythonPath = @"C:\Py\python.exe";
        private const string PythonVersion = "3.11.9";

        private string tempDirectory;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "FlowDependencyMarkerTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Test]
        public void ComputeKey_SameInputs_SameKey()
        {
            var first = DependencyInstallMarker.ComputeKey(Dependencies, PythonPath, PythonVersion);
            var second = DependencyInstallMarker.ComputeKey(Dependencies, PythonPath, PythonVersion);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void ComputeKey_IsSixteenLowerHex()
        {
            var key = DependencyInstallMarker.ComputeKey(Dependencies, PythonPath, PythonVersion);

            Assert.That(key, Does.Match("^[0-9a-f]{16}$"));
        }

        [Test]
        public void ComputeKey_DifferentDependencies_DifferentKey()
        {
            var first = DependencyInstallMarker.ComputeKey(Dependencies, PythonPath, PythonVersion);
            var second = DependencyInstallMarker.ComputeKey(new[] { "requests==2.32.4", "rich==13.7.1" }, PythonPath, PythonVersion);

            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void ComputeKey_DifferentPythonVersion_DifferentKey()
        {
            var first = DependencyInstallMarker.ComputeKey(Dependencies, PythonPath, PythonVersion);
            var second = DependencyInstallMarker.ComputeKey(Dependencies, PythonPath, "3.12.1");

            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void ComputeKey_DifferentPythonPath_DifferentKey()
        {
            var first = DependencyInstallMarker.ComputeKey(Dependencies, PythonPath, PythonVersion);
            var second = DependencyInstallMarker.ComputeKey(Dependencies, @"C:\Other\python.exe", PythonVersion);

            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void ComputeKey_PythonPathCaseInsensitive()
        {
            var first = DependencyInstallMarker.ComputeKey(Dependencies, @"C:\Py\python.exe", PythonVersion);
            var second = DependencyInstallMarker.ComputeKey(Dependencies, @"c:\py\PYTHON.EXE", PythonVersion);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void SaveThenLoad_RoundTrips()
        {
            var markerPath = Path.Combine(tempDirectory, DependencyInstallMarker.FileName);
            var marker = new DependencyInstallMarker(
                "0123456789abcdef",
                Dependencies,
                PythonPath,
                PythonVersion,
                "0.13.7",
                new[] { "requests==2.32.3", "urllib3==2.2.1" },
                "0123456789abcdef-89abcdef");

            marker.Save(markerPath);
            var loaded = DependencyInstallMarker.Load(markerPath);

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.InstallKey, Is.EqualTo(marker.InstallKey));
            Assert.That(loaded.Dependencies, Is.EqualTo(marker.Dependencies));
            Assert.That(loaded.PythonPath, Is.EqualTo(marker.PythonPath));
            Assert.That(loaded.PythonVersion, Is.EqualTo(marker.PythonVersion));
            Assert.That(loaded.UvVersion, Is.EqualTo(marker.UvVersion));
            Assert.That(loaded.Resolved, Is.EqualTo(marker.Resolved));
            Assert.That(loaded.InstallFolder, Is.EqualTo(marker.InstallFolder));
            Assert.That(File.Exists(markerPath + ".tmp"), Is.False);
        }

        [Test]
        public void Matches_ComparesInstallKeyOrdinally()
        {
            var marker = new DependencyInstallMarker(
                "abcdef0123456789", Dependencies, PythonPath, PythonVersion, "0.13.7", Array.Empty<string>(), "abcdef0123456789-00000000");

            Assert.That(marker.Matches("abcdef0123456789"), Is.True);
            Assert.That(marker.Matches("ABCDEF0123456789"), Is.False);
        }

        [Test]
        public void Load_Missing_ReturnsNull()
        {
            var markerPath = Path.Combine(tempDirectory, DependencyInstallMarker.FileName);

            Assert.That(DependencyInstallMarker.Load(markerPath), Is.Null);
        }

        [Test]
        public void Load_InvalidJson_ReturnsNull()
        {
            var markerPath = Path.Combine(tempDirectory, DependencyInstallMarker.FileName);
            File.WriteAllText(markerPath, "{not json");

            Assert.That(DependencyInstallMarker.Load(markerPath), Is.Null);
        }

        [Test]
        public void NewInstallFolder_IsKeyAndEightLowerHex_DiffersEachCall()
        {
            var first = DependencyInstallMarker.NewInstallFolder("0123456789abcdef");
            var second = DependencyInstallMarker.NewInstallFolder("0123456789abcdef");

            Assert.That(first, Does.Match("^0123456789abcdef-[0-9a-f]{8}$"));
            Assert.That(second, Is.Not.EqualTo(first));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("0123456789abcdef")]
        [TestCase("fedcba9876543210-89abcdef")]
        [TestCase("0123456789abcdef-89ABCDEF")]
        [TestCase(@"0123456789abcdef-89abcdef\..\..")]
        [TestCase("..")]
        public void Load_InstallFolderMissingOrNotForItsKey_ReturnsNull(string installFolder)
        {
            var markerPath = Path.Combine(tempDirectory, DependencyInstallMarker.FileName);
            File.WriteAllText(markerPath, JsonSerializer.Serialize(new { InstallKey = "0123456789abcdef", InstallFolder = installFolder }));

            Assert.That(DependencyInstallMarker.Load(markerPath), Is.Null);
        }

        [Test]
        public void Load_MarkerWithoutInstallFolderField_ReturnsNull()
        {
            var markerPath = Path.Combine(tempDirectory, DependencyInstallMarker.FileName);
            File.WriteAllText(markerPath, """{"InstallKey":"0123456789abcdef","Dependencies":["a==1"],"PythonPath":"p","PythonVersion":"3.11.9","UvVersion":"uv 0.13.7","Resolved":["a==1"]}""");

            Assert.That(DependencyInstallMarker.Load(markerPath), Is.Null);
        }

        [Test]
        public void Load_EmptyObject_ReturnsNull()
        {
            var markerPath = Path.Combine(tempDirectory, DependencyInstallMarker.FileName);
            File.WriteAllText(markerPath, "{}");

            Assert.That(DependencyInstallMarker.Load(markerPath), Is.Null);
        }
    }
}
