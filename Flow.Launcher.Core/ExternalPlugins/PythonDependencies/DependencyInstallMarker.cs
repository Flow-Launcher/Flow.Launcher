#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Core.ExternalPlugins.PythonDependencies
{
    internal sealed record DependencyInstallMarker(
        string InstallKey,
        IReadOnlyList<string> Dependencies,
        string PythonPath,
        string PythonVersion,
        string UvVersion,
        IReadOnlyList<string> Resolved,
        string InstallFolder)
    {
        public const string FileName = "install.json";

        private const int KeyLength = 16;
        private const int FolderSuffixBytes = 4;

        private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

        public static string ComputeKey(IReadOnlyList<string> normalizedDependencies, string resolvedPythonPath, string pythonVersion)
        {
            var input = string.Join("\n", normalizedDependencies)
                + "\n\0" + resolvedPythonPath.ToUpperInvariant()
                + "\n\0" + pythonVersion;
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hash).ToLowerInvariant().Substring(0, KeyLength);
        }

        public static string NewInstallFolder(string installKey)
        {
            return installKey + "-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(FolderSuffixBytes)).ToLowerInvariant();
        }

        public static DependencyInstallMarker? Load(string markerPath)
        {
            try
            {
                var marker = JsonSerializer.Deserialize<DependencyInstallMarker>(File.ReadAllText(markerPath));
                return marker is not null && IsInstallFolderOf(marker.InstallFolder, marker.InstallKey) ? marker : null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                return null;
            }
        }

        public void Save(string markerPath)
        {
            var temporaryPath = markerPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(this, WriteOptions));
            File.Move(temporaryPath, markerPath, overwrite: true);
        }

        public bool Matches(string installKey)
        {
            return string.Equals(InstallKey, installKey, StringComparison.Ordinal);
        }

        private static bool IsInstallFolderOf(string? installFolder, string? installKey)
        {
            return !string.IsNullOrEmpty(installKey)
                && installFolder is not null
                && Regex.IsMatch(installFolder, "^" + Regex.Escape(installKey) + "-[0-9a-f]{" + FolderSuffixBytes * 2 + "}$");
        }
    }
}
