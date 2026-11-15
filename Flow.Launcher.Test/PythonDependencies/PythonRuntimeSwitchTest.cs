using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Flow.Launcher.Core.ExternalPlugins.PythonDependencies;
using NUnit.Framework;

namespace Flow.Launcher.Test.PythonDependencies
{
    [TestFixture]
    public class PythonRuntimeSwitchTest
    {
        private string envPath;
        private List<string> events;
        private List<string> logged;

        [SetUp]
        public void SetUp()
        {
            envPath = Path.Combine(Path.GetTempPath(), "FlowRuntimeSwitchTest_" + Guid.NewGuid().ToString("N"), "Python");
            Directory.CreateDirectory(envPath);
            events = new List<string>();
            logged = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            var root = Path.GetDirectoryName(envPath);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }

        private string MakeFolder(string name)
        {
            var path = Path.Combine(envPath, name);
            Directory.CreateDirectory(Path.Combine(path, "inner"));
            File.WriteAllText(Path.Combine(path, "inner", "file.txt"), "x");
            return path;
        }

        [Test]
        public async Task Install_Succeeds_InstallsThenSetsPath()
        {
            var switched = await PythonRuntimeSwitch.TryAsync(
                () =>
                {
                    events.Add("install");
                    return Task.FromResult("new.exe");
                },
                path => events.Add("set " + path),
                _ => events.Add("failed"));

            Assert.That(switched, Is.True);
            Assert.That(events, Is.EqualTo(new[] { "install", "set new.exe" }));
        }

        [Test]
        public async Task Install_Fails_KeepsOldPath()
        {
            var failure = new InvalidOperationException("offline");
            Exception reported = null;

            var switched = await PythonRuntimeSwitch.TryAsync(
                () => throw failure,
                path => events.Add("set " + path),
                e => reported = e);

            Assert.That(switched, Is.False);
            Assert.That(events, Is.Empty);
            Assert.That(reported, Is.SameAs(failure));
        }

        [Test]
        public void RemoveStale_KeepsRuntimeAndDependencies_RemovesEverythingElse()
        {
            var runtime = MakeFolder("Runtime");
            var dependencies = MakeFolder("PluginDependencies");
            var removals = MakeFolder("PluginDependencies.removals");
            var embedded = MakeFolder("PythonEmbeddable-v3.11.4");
            var other = MakeFolder("SomethingElse");
            var strayFile = Path.Combine(envPath, "stray.txt");
            File.WriteAllText(strayFile, "x");

            PythonRuntimeSwitch.RemoveStale(envPath, logged.Add);

            Assert.That(Directory.Exists(runtime), Is.True);
            Assert.That(Directory.Exists(dependencies), Is.True);
            Assert.That(Directory.Exists(removals), Is.True);
            Assert.That(Directory.Exists(embedded), Is.False);
            Assert.That(Directory.Exists(other), Is.False);
            Assert.That(File.Exists(strayFile), Is.False);
            Assert.That(logged, Is.Empty);
        }

        [Test]
        public void RemoveStale_KeepNamesIgnoreCase()
        {
            var runtime = MakeFolder("runtime");
            var dependencies = MakeFolder("plugindependencies");

            PythonRuntimeSwitch.RemoveStale(envPath, logged.Add);

            Assert.That(Directory.Exists(runtime), Is.True);
            Assert.That(Directory.Exists(dependencies), Is.True);
        }

        [Test]
        public void RemoveStale_FolderMissing_DoesNothing()
        {
            Directory.Delete(envPath);

            Assert.DoesNotThrow(() => PythonRuntimeSwitch.RemoveStale(envPath, logged.Add));
            Assert.That(logged, Is.Empty);
        }

        [Test]
        public void RemoveStale_DeleteFails_LogsAndContinues()
        {
            var first = MakeFolder("PythonEmbeddable-v3.11.4");
            var second = MakeFolder("Other");

            PythonRuntimeSwitch.RemoveStale(
                envPath,
                logged.Add,
                path =>
                {
                    if (path == first)
                    {
                        throw new IOException("in use");
                    }

                    Directory.Delete(path, recursive: true);
                });

            Assert.That(Directory.Exists(first), Is.True);
            Assert.That(Directory.Exists(second), Is.False);
            Assert.That(logged.Single(), Does.Contain("in use").And.Contain(first));
        }
    }
}
