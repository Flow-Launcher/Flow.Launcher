using System;
using System.IO;
using System.Reflection;
using Flow.Launcher.Plugin.BrowserBookmark;
using NUnit.Framework;

namespace Flow.Launcher.Test.Plugins
{
    [TestFixture]
    public class BrowserBookmarkPluginTest
    {
        [TestCase("\n")]
        [TestCase("\r\n")]
        public void GetProfileIniPath_ParsesDefaultProfileWithDifferentLineEndings(string newline)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var profileRoot = Path.Combine(tempDir, "Mozilla", "Firefox");
                Directory.CreateDirectory(profileRoot);

                var defaultProfile = "Profiles/7789f565.default-release";
                var lines = new[]
                {
                    "[Install736426B0AF4A39CB]",
                    $"Default={defaultProfile}",
                    "Locked=1",
                    string.Empty,
                    "[Profile0]",
                    "Name=default-release",
                    "IsRelative=1",
                    $"Path={defaultProfile}"
                };

                File.WriteAllText(Path.Combine(profileRoot, "profiles.ini"), string.Join(newline, lines));

                var method = typeof(FirefoxBookmarkLoader).GetMethod("GetProfileIniPath",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(method, Is.Not.Null);

                var path = method.Invoke(null, new object[] { profileRoot }) as string;
                var expected = Path.Combine(profileRoot, defaultProfile, "places.sqlite");

                Assert.That(path, Is.EqualTo(expected));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}
