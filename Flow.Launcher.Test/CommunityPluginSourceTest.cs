using Flow.Launcher.Core.ExternalPlugins;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test;

public class CommunityPluginSourceTest
{
    [Test]
    public void ManifestFileUrlForLogging_OmitsCredentialsQueryAndFragment()
    {
        const string manifestUrl =
            "https://username:password@example.com:8443/private/plugins.json?token=secret#fragment";
        var source = new CommunityPluginSource(manifestUrl);

        ClassicAssert.AreEqual(manifestUrl, source.ManifestFileUrl);
        ClassicAssert.AreEqual(
            "https://example.com:8443/private/plugins.json",
            source.ManifestFileUrlForLogging);
    }
}
