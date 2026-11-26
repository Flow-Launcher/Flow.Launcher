using System.Collections.Generic;
using System.Text.Json;
using Flow.Launcher.Plugin;
using NUnit.Framework;

namespace Flow.Launcher.Test.PythonDependencies
{
    [TestFixture]
    public class PluginMetadataDependenciesTest
    {
        [Test]
        public void Deserialize_WithDependencies()
        {
            var metadata = JsonSerializer.Deserialize<PluginMetadata>("{\"Dependencies\":[\"a==1\"]}");

            Assert.That(metadata.Dependencies, Is.EqualTo(new List<string> { "a==1" }));
        }

        [Test]
        public void Deserialize_WithoutDependencies_IsEmptyNotNull()
        {
            var metadata = JsonSerializer.Deserialize<PluginMetadata>("{}");

            Assert.That(metadata.Dependencies, Is.Not.Null);
            Assert.That(metadata.Dependencies, Is.Empty);
        }
    }
}
