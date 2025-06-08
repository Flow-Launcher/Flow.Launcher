using System;
using System.IO;

namespace Flow.Launcher.Plugin.BrowserBookmark.Helper;

public static class FaviconHelper
{
    private static readonly string ClassName = nameof(FaviconHelper);

    public static void LoadFaviconsFromDb(string faviconCacheDir, string dbPath, Action<string> loadAction)
    {
        // Use a copy to avoid lock issues with the original file
        var tempDbPath = Path.Combine(faviconCacheDir, $"tempfavicons_{Guid.NewGuid()}.db");

        try
        {
            File.Copy(dbPath, tempDbPath, true);
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(tempDbPath))
                {
                    File.Delete(tempDbPath);
                }
            }
            catch (Exception ex1)
            {
                Main._context.API.LogException(ClassName, $"Failed to delete temporary favicon DB: {tempDbPath}", ex1);
            }
            Main._context.API.LogException(ClassName, $"Failed to copy favicon DB: {dbPath}", ex);
            return;
        }

        try
        {
            loadAction(tempDbPath);
        }
        catch (Exception ex)
        {
            Main._context.API.LogException(ClassName, $"Failed to connect to SQLite: {tempDbPath}", ex);
        }

        // Delete temporary file
        try
        {
            File.Delete(tempDbPath);
        }
        catch (Exception ex)
        {
            Main._context.API.LogException(ClassName, $"Failed to delete temporary favicon DB: {tempDbPath}", ex);
        }
    }

    public static void SaveBitmapData(byte[] imageData, string outputPath)
    {
        try
        {
            File.WriteAllBytes(outputPath, imageData);
        }
        catch (Exception ex)
        {
            Main._context.API.LogException(ClassName, $"Failed to save image: {outputPath}", ex);
        }
    }

    public static bool IsSvgData(byte[] data)
    {
        if (data.Length < 5)
            return false;
        string start = System.Text.Encoding.ASCII.GetString(data, 0, Math.Min(100, data.Length));
        return start.Contains("<svg") ||
               (start.StartsWith("<?xml") && start.Contains("<svg"));
    }
}
