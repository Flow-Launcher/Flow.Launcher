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

        // HttpClient's timeout stops at the response headers, so a stalled body would otherwise never end.
        private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);

        public static async Task<string> DownloadAndExtractAsync(string installDir, CancellationToken token)
        {
            var (asset, expectedSha256) = UvRelease.ForArchitecture(RuntimeInformation.OSArchitecture);
            var zipPath = Path.Combine(Path.GetTempPath(), $"flow-uv-{Guid.NewGuid():N}.zip");
            try
            {
                await DownloadAsync(UvRelease.DownloadUrl(asset), zipPath, token).ConfigureAwait(false);
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

        private static async Task DownloadAsync(string url, string zipPath, CancellationToken token)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(DownloadTimeout);
            try
            {
                await Http.DownloadAsync(url, zipPath, token: deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException e) when (deadline.IsCancellationRequested && !token.IsCancellationRequested)
            {
                throw new TimeoutException($"Downloading uv did not finish within {DownloadTimeout}", e);
            }
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
