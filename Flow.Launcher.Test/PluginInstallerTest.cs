using Flow.Launcher.Core.Plugin;
using NUnit.Framework;

namespace Flow.Launcher.Test;

public class PluginInstallerTest
{
    [TestCase("1.9.0", "1.10.0", true)]
    [TestCase("1.10.0", "1.9.0", false)]
    [TestCase("1.0.0", "1.0.0", false)]
    [TestCase("1", "1.0.0", false)]
    [TestCase("1.0", "1.0.0", false)]
    [TestCase("1.9", "1.10", true)]
    [TestCase("1.0.0-beta", "1.0", true)]
    [TestCase("1.0+build.1", "1.0.0+build.2", false)]
    [TestCase("1.0.0.0", "2", true)]
    [TestCase("custom-1", "custom-2", true)]
    [TestCase("custom-2", "custom-1", false)]
    [TestCase("custom", "custom", false)]
    [TestCase("!custom", "2.0.0", true)]
    [TestCase("1.0.0", "custom", true)]
    public void IsUpdateAvailableComparesSemanticAndNonSemanticVersions(
        string currentVersion,
        string latestVersion,
        bool expected)
    {
        Assert.That(PluginInstaller.IsUpdateAvailable(currentVersion, latestVersion), Is.EqualTo(expected));
    }
}
