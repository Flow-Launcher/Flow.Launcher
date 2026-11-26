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

        private static string DependencyLines(string sitePackages) =>
            $$"""
             import site
             def _flow_add_dependencies(path):
                 import os
                 before = list(sys.path)
                 site.addsitedir(path)
                 added = [entry for entry in sys.path if entry not in before]
                 try:
                     normalize = lambda entry: os.path.normcase(os.path.abspath(entry))
                     interpreter_dirs = {normalize(entry) for entry in site.getsitepackages() + [site.getusersitepackages()]}
                     at = next((i for i, entry in enumerate(before) if normalize(entry) in interpreter_dirs), len(before))
                 except Exception:
                     at = len(before)
                 sys.path[:] = before[:at] + added + before[at:]
             _flow_add_dependencies({{Quoted(sitePackages)}})
             del _flow_add_dependencies
             """;

        [Test]
        public void Py_WithDependencies_PrependsDependenciesBetweenRootAndLib()
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
                 {DependencyLines(SitePackages)}
                 sys.path.append({Quoted(libDirectory)})
                 sys.path.append({Quoted(libPyWin32LibDirectory)})
                 sys.path.append({Quoted(libPyWin32Directory)})
                 sys.path.append({Quoted(pluginDirectory)})

                 import runpy
                 runpy.run_path({Quoted(metadata.ExecuteFilePath)}, None, '__main__')
                 """;

            var arguments = PythonLaunchArguments.Build(metadata, SitePackages, v1: true);

            Assert.That(arguments, Has.Count.EqualTo(2));
            Assert.That(arguments[0], Is.EqualTo("-c"));
            Assert.That(Normalize(arguments[1]), Is.EqualTo(Normalize(expected)));
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
            Assert.That(snippet, Does.Contain(@"_flow_add_dependencies('C:\\Users\\O\'Brien José\\Flow\\site-packages')"));
            Assert.That(snippet, Does.Not.Contain("r'"));
        }

        [Test]
        public void Pyz_WithDependencies_EscapesApostropheInPaths()
        {
            var metadata = CreateMetadata("plugin.pyz");
            metadata.PluginDirectory = @"C:\Users\O'Brien José\Plugins";
            var sitePackages = @"C:\Users\O'Brien José\Flow\site-packages";

            var snippet = PythonLaunchArguments.Build(metadata, sitePackages, v1: false)[1];

            Assert.That(snippet, Does.Contain(@"_flow_add_dependencies('C:\\Users\\O\'Brien José\\Flow\\site-packages')"));
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

            var expected =
                $"""
                 import sys
                 {DependencyLines(SitePackages)}

                 import runpy
                 runpy.run_path({Quoted(metadata.ExecuteFilePath)}, None, '__main__')
                 """;

            Assert.That(arguments, Has.Count.EqualTo(2));
            Assert.That(arguments[0], Is.EqualTo("-c"));
            Assert.That(Normalize(arguments[1]), Is.EqualTo(Normalize(expected)));
        }
    }
}
