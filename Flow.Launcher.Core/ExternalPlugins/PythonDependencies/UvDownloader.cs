#nullable enable

using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Http;

namespace Flow.Launcher.Core.ExternalPlugins.PythonDependencies
{
    internal static class UvDownloader
    {
        private const string ExecutableName = "uv.exe";

        public static async Task<string> DownloadAndExtractAsync(string installDir, CancellationToken token)
        {
            var (asset, expectedSha256) = UvRelease.ForArchitecture(RuntimeInformation.OSArchitecture);
            var zipPath = Path.Combine(Path.GetTempPath(), $"flow-uv-{Guid.NewGuid():N}.zip");
            try
            {
                await Http.DownloadAsync(UvRelease.DownloadUrl(asset), zipPath, token: token).ConfigureAwait(false);
                VerifySha256(zipPath, expectedSha256);

                Directory.CreateDirectory(installDir);
                ZipFile.ExtractToDirectory(zipPath, installDir, overwriteFiles: true);
            }
            finally
            {
                TryDelete(zipPath);
            }

            var uvPath = Path.Combine(installDir, ExecutableName);
            if (!File.Exists(uvPath))
            {
                throw new FileNotFoundException($"{ExecutableName} was not found in the downloaded uv archive.", uvPath);
            }

            return uvPath;
        }

        public static void VerifySha256(string filePath, string expectedHex)
        {
            using var stream = File.OpenRead(filePath);
            var actualHex = Convert.ToHexString(SHA256.HashData(stream));
            if (!string.Equals(actualHex, expectedHex, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"SHA-256 mismatch for {Path.GetFileName(filePath)}: expected {expectedHex.ToLowerInvariant()}, actual {actualHex.ToLowerInvariant()}.");
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception)
            {
                // A leftover temp file is harmless.
            }
        }
    }
}
