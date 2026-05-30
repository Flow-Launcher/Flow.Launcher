using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flow.Launcher.Plugin.Program.Programs;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test
{
    [TestFixture]
    internal class UwpPackageLogoTest
    {
        [Test]
        public void GivenLocalizedUwpLogoAsset_WhenResolvingManifestLogo_ThenNestedAssetIsReturned()
        {
            var packageDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, Path.GetRandomFileName());
            var localizedAssetsDirectory = Path.Combine(packageDirectory, "Assets", "App", "en-US");
            Directory.CreateDirectory(localizedAssetsDirectory);
            var expectedLogo = Path.Combine(localizedAssetsDirectory, "Square44x44Logo.scale-200.png");
            CreatePng(expectedLogo, width: 44, height: 44);

            try
            {
                var app = CreateUwpApp(packageDirectory);

                var logoPath = ResolveLogoPath(app, "App\\Square44x44Logo.png", (64, 64));

                ClassicAssert.AreEqual(expectedLogo, logoPath);
            }
            finally
            {
                Directory.Delete(packageDirectory, recursive: true);
            }
        }

        [Test]
        public void GivenMissingUwpLogoAsset_WhenResolvingManifestLogo_ThenFallsBackWithoutErrorLog()
        {
            var packageDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, Path.GetRandomFileName());
            Directory.CreateDirectory(packageDirectory);
            var previousConfig = LogManager.Configuration;
            var memoryTarget = new MemoryTarget("uwp-logo-memory") { Layout = "${level}|${message}" };
            var config = new LoggingConfiguration();
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, memoryTarget);
            LogManager.Configuration = config;

            try
            {
                var app = CreateUwpApp(packageDirectory);

                var logoPath = ResolveLogoPath(app, "Missing\\Square44x44Logo.png", (64, 64));
                LogManager.Flush();

                ClassicAssert.AreEqual(string.Empty, logoPath);
                ClassicAssert.IsFalse(
                    memoryTarget.Logs.Any(log => log.Contains("|UWP|LogoPathFromUri|", StringComparison.Ordinal)),
                    string.Join(Environment.NewLine, memoryTarget.Logs));
            }
            finally
            {
                LogManager.Configuration = previousConfig;
                Directory.Delete(packageDirectory, recursive: true);
            }
        }

        [Test]
        public void GivenMalformedUwpLogoAsset_WhenResolvingManifestLogo_ThenFallsBackWithoutThrowing()
        {
            var packageDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, Path.GetRandomFileName());
            var assetsDirectory = Path.Combine(packageDirectory, "Assets");
            Directory.CreateDirectory(assetsDirectory);
            var malformedLogo = Path.Combine(assetsDirectory, "Square44x44Logo.scale-200.png");
            File.WriteAllText(malformedLogo, "not a png");

            try
            {
                var app = CreateUwpApp(packageDirectory);

                var logoPath = ResolveLogoPath(app, "Square44x44Logo.png", (64, 64));

                ClassicAssert.AreEqual(string.Empty, logoPath);
            }
            finally
            {
                Directory.Delete(packageDirectory, recursive: true);
            }
        }

        private static UWPApp CreateUwpApp(string packageDirectory)
        {
            var app = (UWPApp)Activator.CreateInstance(typeof(UWPApp), nonPublic: true);
            app.Location = packageDirectory;
            app.UserModelId = "Package!App";
            return app;
        }

        private static string ResolveLogoPath(UWPApp app, string uri, (int, int) desiredSize)
        {
            var method = typeof(UWPApp).GetMethod("LogoPathFromUri", BindingFlags.Instance | BindingFlags.NonPublic);
            return (string)method.Invoke(app, [uri, desiredSize]);
        }

        private static void CreatePng(string path, int width, int height)
        {
            var stride = width * 4;
            var pixels = new byte[stride * height];
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 0x70;
                pixels[i + 1] = 0x90;
                pixels[i + 2] = 0xE0;
                pixels[i + 3] = 0xFF;
            }

            var source = BitmapSource.Create(
                width,
                height,
                dpiX: 96,
                dpiY: 96,
                PixelFormats.Bgra32,
                palette: null,
                pixels,
                stride);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));

            using var stream = File.Create(path);
            encoder.Save(stream);
        }
    }
}
