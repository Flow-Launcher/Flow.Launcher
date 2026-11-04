using System.IO;
using System.Linq;
using System.Text.Json;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;
using NUnit.Framework;

namespace Flow.Launcher.Test.PythonDependencies
{
    [TestFixture]
    public class PythonLaunchArgumentsTest
    {
        private static readonly string PluginDirectory = Path.Combine(Path.GetTempPath(), "Flow Plugins", "My Plugin");
        private static readonly string SitePackages = Path.Combine(Path.GetTempPath(), "Flow Deps", "key", "site-packages");

        private static PluginMetadata CreateMetadata(string executeFileName)
        {
            var metadata = JsonSerializer.Deserialize<PluginMetadata>(
                JsonSerializer.Serialize(new { ExecuteFileName = executeFileName, IcoPath = "icon.png" }));
            metadata.PluginDirectory = PluginDirectory;
            return metadata;
        }

        private static string Normalize(string text) => text.Replace("\r\n", "\n");

        private static string Quoted(string path) => "'" + path.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

        [Test]
        public void Py_NoDependencies_SnippetUnchanged()
        {
            var metadata = CreateMetadata("main.py");
            var rootDirectory = metadata.PluginDirectory;
            var libDirectory = Path.Combine(rootDirectory, "lib");
            var libPyWin32Directory = Path.Combine(libDirectory, "win32");
            var libPyWin32LibDirectory = Path.Combine(libPyWin32Directory, "lib");
            var pluginDirectory = Path.Combine(rootDirectory, "plugin");
            var expected =
                $"""
                 import sys
                 sys.path.append({Quoted(rootDirectory)})
                 sys.path.append({Quoted(libDirectory)})
                 sys.path.append({Quoted(libPyWin32LibDirectory)})
                 sys.path.append({Quoted(libPyWin32Directory)})
                 sys.path.append({Quoted(pluginDirectory)})

                 import runpy
                 runpy.run_path({Quoted(metadata.ExecuteFilePath)}, None, '__main__')
                 """;

            var arguments = PythonLaunchArguments.Build(metadata, null, v1: true);

            Assert.That(arguments, Has.Count.EqualTo(2));
            Assert.That(arguments[0], Is.EqualTo("-c"));
            Assert.That(Normalize(arguments[1]), Is.EqualTo(Normalize(expected)));
        }

        [Test]
        public void Py_NoDependencies_V1AndV2AreIdentical()
        {
            var metadata = CreateMetadata("main.py");

            Assert.That(PythonLaunchArguments.Build(metadata, null, v1: false),
                Is.EqualTo(PythonLaunchArguments.Build(metadata, null, v1: true)));
        }

        [Test]
        public void Py_WithDependencies_AddSiteDirBetweenRootAndLib()
        {
            var metadata = CreateMetadata("main.py");

            var arguments = PythonLaunchArguments.Build(metadata, SitePackages, v1: true);

            Assert.That(arguments, Has.Count.EqualTo(2));
            Assert.That(arguments[0], Is.EqualTo("-c"));
            var snippet = arguments[1];
            var rootAppend = snippet.IndexOf($"sys.path.append({Quoted(metadata.PluginDirectory)})", System.StringComparison.Ordinal);
            var siteDir = snippet.IndexOf("addsitedir('", System.StringComparison.Ordinal);
            var libAppend = snippet.IndexOf($"sys.path.append({Quoted(Path.Combine(metadata.PluginDirectory, "lib"))})", System.StringComparison.Ordinal);
            Assert.That(rootAppend, Is.GreaterThanOrEqualTo(0));
            Assert.That(siteDir, Is.GreaterThan(rootAppend));
            Assert.That(libAppend, Is.GreaterThan(siteDir));
            Assert.That(snippet, Does.Contain($"site.addsitedir({Quoted(SitePackages)})"));
            Assert.That(snippet.IndexOf("import site", System.StringComparison.Ordinal), Is.LessThan(siteDir));
            Assert.That(snippet, Does.EndWith($"runpy.run_path({Quoted(metadata.ExecuteFilePath)}, None, '__main__')"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Py_WithDependencies_EscapesBackslashesQuotesAndKeepsNonAscii(bool v1)
        {
            var metadata = CreateMetadata("main.py");
            metadata.PluginDirectory = @"C:\Users\O'Brien José\Plugins";
            var sitePackages = @"C:\Users\O'Brien José\Flow\site-packages";

            var snippet = PythonLaunchArguments.Build(metadata, sitePackages, v1)[1];

            Assert.That(snippet, Does.Contain(@"sys.path.append('C:\\Users\\O\'Brien José\\Plugins')"));
            Assert.That(snippet, Does.Contain(@"site.addsitedir('C:\\Users\\O\'Brien José\\Flow\\site-packages')"));
            Assert.That(snippet, Does.Not.Contain("r'"));
        }

        [Test]
        public void Pyz_WithDependencies_EscapesApostropheInPaths()
        {
            var metadata = CreateMetadata("plugin.pyz");
            metadata.PluginDirectory = @"C:\Users\O'Brien José\Plugins";
            var sitePackages = @"C:\Users\O'Brien José\Flow\site-packages";

            var snippet = PythonLaunchArguments.Build(metadata, sitePackages, v1: false)[1];

            Assert.That(snippet, Does.Contain(@"site.addsitedir('C:\\Users\\O\'Brien José\\Flow\\site-packages')"));
            Assert.That(snippet, Does.Contain(@"runpy.run_path('C:\\Users\\O\'Brien José\\Plugins"));
        }

        [Test]
        public void Pyz_NoDependencies_V1IsDashBAndPath()
        {
            var metadata = CreateMetadata("plugin.pyz");

            var arguments = PythonLaunchArguments.Build(metadata, null, v1: true);

            Assert.That(arguments, Is.EqualTo(new[] { "-B", metadata.ExecuteFilePath }));
        }

        [Test]
        public void Pyz_NoDependencies_V2IsPathOnly()
        {
            var metadata = CreateMetadata("plugin.pyz");

            var arguments = PythonLaunchArguments.Build(metadata, null, v1: false);

            Assert.That(arguments, Is.EqualTo(new[] { metadata.ExecuteFilePath }));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Pyz_WithDependencies_RunsThroughRunpy(bool v1)
        {
            var metadata = CreateMetadata("plugin.pyz");

            var arguments = PythonLaunchArguments.Build(metadata, SitePackages, v1);

            Assert.That(arguments, Has.Count.EqualTo(2));
            Assert.That(arguments[0], Is.EqualTo("-c"));
            Assert.That(arguments[1], Does.Contain($"site.addsitedir({Quoted(SitePackages)})"));
            Assert.That(arguments[1], Does.EndWith($"runpy.run_path({Quoted(metadata.ExecuteFilePath)}, None, '__main__')"));
        }
    }
}
