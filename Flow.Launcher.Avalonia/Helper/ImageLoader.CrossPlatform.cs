using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// macOS icons: .icns files (and .app bundles via their CFBundleIconFile) are converted once to PNG with sips
/// and kept in the Flow cache directory. Other files fall back to the default icon, folders to the folder icon.
/// </summary>
public static partial class ImageLoader
{
    private const string SipsCommand = "/usr/bin/sips";
    private const string PlutilCommand = "/usr/bin/plutil";
    private const string IcnsExtension = ".icns";
    private const int IconPixelSize = 128;
    private static readonly TimeSpan ToolTimeout = TimeSpan.FromSeconds(10);

    private static readonly string IconCacheDirectory = Path.Combine(DataLocation.CacheDirectory, "Icons");

    private static IImage? _folderImage;

    private static IImage? FolderImage => _folderImage ??= LoadBundledImage("folder.png");

    private static partial IImage? LoadPlatformIcon(string path)
    {
        // Skia decodes .ico; everything else has no cheap platform icon
        if (!Path.GetExtension(path).Equals(".ico", StringComparison.OrdinalIgnoreCase))
            return null;

        using var stream = File.OpenRead(path);
        return new Bitmap(stream);
    }

    private static partial async Task<IImage?> LoadPlatformIconAsync(string path)
    {
        if (Directory.Exists(path))
        {
            if (!path.TrimEnd('/').EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                return FolderImage;

            var bundleIcon = await GetBundleIconPathAsync(path);
            return bundleIcon == null ? null : await LoadIcnsAsync(bundleIcon);
        }

        if (Path.GetExtension(path).Equals(IcnsExtension, StringComparison.OrdinalIgnoreCase))
            return await LoadIcnsAsync(path);

        return LoadPlatformIcon(path);
    }

    /// <summary>
    /// Loads an .icns through a PNG rendition cached on disk; the cache key covers path, size and modification
    /// time so updated apps get a fresh icon.
    /// </summary>
    private static async Task<IImage?> LoadIcnsAsync(string icnsPath)
    {
        var file = new FileInfo(icnsPath);
        if (!file.Exists)
            return null;

        var cacheKey = $"{file.FullName}\n{file.Length}\n{file.LastWriteTimeUtc.Ticks}\n{IconPixelSize}";
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey)), 0, 16);
        var pngPath = Path.Combine(IconCacheDirectory, $"{hash}.png");

        if (!File.Exists(pngPath))
        {
            Directory.CreateDirectory(IconCacheDirectory);

            // Convert to a unique temp file so concurrent loads never observe a half-written PNG
            var tempPath = Path.Combine(IconCacheDirectory, $"{hash}.{Guid.NewGuid():N}.tmp.png");
            try
            {
                var (exitCode, _, error) = await RunToolAsync(SipsCommand,
                    "-s", "format", "png", "-Z", IconPixelSize.ToString(), file.FullName, "--out", tempPath);
                if (exitCode != 0 || !File.Exists(tempPath))
                {
                    Log.Debug(ClassName, $"sips failed to convert {icnsPath} (exit {exitCode}): {error.Trim()}");
                    return null;
                }

                File.Move(tempPath, pngPath, overwrite: true);
            }
            finally
            {
                File.Delete(tempPath);
            }
        }

        await using var stream = File.OpenRead(pngPath);
        return new Bitmap(stream);
    }

    /// <summary>
    /// Resolves the bundle's CFBundleIconFile to Contents/Resources/&lt;name&gt;[.icns], or null if it has none
    /// (e.g. apps that only ship an asset catalog).
    /// </summary>
    private static async Task<string?> GetBundleIconPathAsync(string bundlePath)
    {
        var plistPath = Path.Combine(bundlePath, "Contents", "Info.plist");
        if (!File.Exists(plistPath))
            return null;

        // plutil reads XML and binary plists alike; it exits non-zero when the key is missing
        var (exitCode, output, _) = await RunToolAsync(PlutilCommand, "-extract", "CFBundleIconFile", "raw", "-o", "-", plistPath);
        var iconFile = output.Trim();
        if (exitCode != 0 || iconFile.Length == 0)
            return null;

        var iconPath = Path.Combine(bundlePath, "Contents", "Resources", iconFile);
        if (File.Exists(iconPath))
            return iconPath;

        // CFBundleIconFile may omit the extension
        iconPath += IcnsExtension;
        return File.Exists(iconPath) ? iconPath : null;
    }

    // Limits concurrent sips/plutil processes when many uncached rows appear at once.
    private static readonly SemaphoreSlim ToolGate = new(Math.Clamp(Environment.ProcessorCount / 2, 2, 4));

    private static async Task<(int ExitCode, string Output, string Error)> RunToolAsync(string command, params string[] arguments)
    {
        var info = new ProcessStartInfo(command)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        await ToolGate.WaitAsync().ConfigureAwait(false);
        try
        {
            using var process = Process.Start(info)!;
            using var timeout = new CancellationTokenSource(ToolTimeout);
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                return (process.ExitCode, await outputTask, await errorTask);
            }
            catch (OperationCanceledException)
            {
                process.Kill();
                return (-1, string.Empty, $"{command} timed out");
            }
        }
        finally
        {
            ToolGate.Release();
        }
    }

    private static IImage? LoadBundledImage(string fileName)
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", fileName);
            if (!File.Exists(path))
                return null;

            using var stream = File.OpenRead(path);
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            Log.Debug(ClassName, $"Failed to load bundled image {fileName}: {ex.Message}");
            return null;
        }
    }
}
