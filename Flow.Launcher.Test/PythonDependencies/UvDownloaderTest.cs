using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Flow.Launcher.Core.ExternalPlugins.PythonDependencies;
using NUnit.Framework;

namespace Flow.Launcher.Test.PythonDependencies
{
    [TestFixture]
    public class UvDownloaderTest
    {
        private const string AbcSha256 = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

        private string filePath;

        [SetUp]
        public void SetUp()
        {
            filePath = Path.Combine(Path.GetTempPath(), "FlowUvDownloaderTest_" + Guid.NewGuid().ToString("N"));
            File.WriteAllBytes(filePath, Encoding.ASCII.GetBytes("abc"));
        }

        [TearDown]
        public void TearDown()
        {
            File.Delete(filePath);
        }

        [Test]
        public void VerifySha256_Mismatch_Throws()
        {
            var expected = new string('0', 64);

            var exception = Assert.Throws<InvalidDataException>(() => UvDownloader.VerifySha256(filePath, expected));

            Assert.That(exception.Message, Does.Contain(expected));
            Assert.That(exception.Message, Does.Contain(AbcSha256));
        }

        [Test]
        public void VerifySha256_Match_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => UvDownloader.VerifySha256(filePath, AbcSha256));
            Assert.DoesNotThrow(() => UvDownloader.VerifySha256(filePath, AbcSha256.ToUpperInvariant()));
        }

        [Test]
        public void UvRelease_ForArchitecture_PicksAssetAndUrl()
        {
            var (x64Asset, x64Sha) = UvRelease.ForArchitecture(Architecture.X64);
            var (armAsset, armSha) = UvRelease.ForArchitecture(Architecture.Arm64);

            Assert.That(UvRelease.Version, Is.EqualTo("0.13.7"));
            Assert.That(x64Asset, Is.EqualTo("uv-x86_64-pc-windows-msvc.zip"));
            Assert.That(x64Sha, Is.EqualTo("0cdc5b38fe5a8168ed6a25814f5ba43c21d313dd3187314fccb37b7cec46e021"));
            Assert.That(armAsset, Is.EqualTo("uv-aarch64-pc-windows-msvc.zip"));
            Assert.That(armSha, Is.EqualTo("505d9e65e7692bac3ddf98c66754aa26c5050c409c184af30a881a0a72cc85e3"));
            Assert.That(UvRelease.DownloadUrl(x64Asset),
                Is.EqualTo("https://github.com/astral-sh/uv/releases/download/0.13.7/uv-x86_64-pc-windows-msvc.zip"));
        }

        [Test]
        public void UvRelease_ForArchitecture_X86_PicksI686Asset()
        {
            var (asset, sha) = UvRelease.ForArchitecture(Architecture.X86);

            Assert.That(asset, Is.EqualTo("uv-i686-pc-windows-msvc.zip"));
            Assert.That(sha, Is.EqualTo("fa2abbf16f7d12cb01c9b7f313d843218fbd91a9d3fab487cc06a1d6765b27bf"));
        }

        [Test]
        public void UvRelease_ForArchitecture_Unsupported_Throws()
        {
            Assert.Throws<PlatformNotSupportedException>(() => UvRelease.ForArchitecture(Architecture.Arm));
        }
    }
}
