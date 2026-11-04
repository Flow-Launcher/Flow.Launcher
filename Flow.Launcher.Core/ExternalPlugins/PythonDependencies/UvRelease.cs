#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Flow.Launcher.Core.ExternalPlugins.PythonDependencies
{
    internal static class UvRelease
    {
        public const string Version = "0.13.7";

        private const string X64Asset = "uv-x86_64-pc-windows-msvc.zip";
        private const string X64Sha256 = "0cdc5b38fe5a8168ed6a25814f5ba43c21d313dd3187314fccb37b7cec46e021";
        private const string Arm64Asset = "uv-aarch64-pc-windows-msvc.zip";
        private const string Arm64Sha256 = "505d9e65e7692bac3ddf98c66754aa26c5050c409c184af30a881a0a72cc85e3";

        public static (string Asset, string Sha256) ForArchitecture(Architecture architecture)
        {
            return architecture == Architecture.Arm64
                ? (Arm64Asset, Arm64Sha256)
                : (X64Asset, X64Sha256);
        }

        public static string DownloadUrl(string asset)
        {
            return $"https://github.com/astral-sh/uv/releases/download/{Version}/{asset}";
        }
    }
}
