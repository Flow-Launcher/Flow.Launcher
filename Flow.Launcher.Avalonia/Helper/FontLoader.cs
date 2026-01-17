using System;
using System.Collections.Concurrent;
using System.IO;
using Avalonia;
using Avalonia.Media;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Helper for loading fonts from file paths for glyph icons.
/// Supports Avalonia's font loading system with custom font files.
/// </summary>
public static class FontLoader
{
    private static readonly ConcurrentDictionary<string, FontFamily> FontFamilyCache = new();
    
    /// <summary>
    /// Get a FontFamily from a GlyphInfo, handling file paths and resource paths.
    /// </summary>
    public static FontFamily? GetFontFamily(GlyphInfo glyph)
    {
        if (glyph == null || string.IsNullOrEmpty(glyph.FontFamily))
            return null;

        var fontFamilyPath = glyph.FontFamily;
        
        if (FontFamilyCache.TryGetValue(fontFamilyPath, out var cached))
            return cached;

        FontFamily? result = null;

        // 1. Try as embedded resource font
        result = TryGetEmbeddedFont(fontFamilyPath);
        
        // 2. Try as system font (Avalonia handles this by name)
        if (result == null)
            result = TryGetSystemFont(fontFamilyPath);
        
        // 3. Try as file path
        if (result == null && IsFilePath(fontFamilyPath))
            result = LoadFontFromFile(fontFamilyPath);

        if (result != null)
            FontFamilyCache[fontFamilyPath] = result;

        return result;
    }

    private static FontFamily? TryGetEmbeddedFont(string fontFamilyPath)
    {
        try
        {
            var fontName = ExtractFontName(fontFamilyPath);
            if (string.IsNullOrEmpty(fontName))
                return null;

            // Check for Segoe Fluent Icons specifically (common for Flow Launcher plugins)
            if (fontName.Contains("Segoe Fluent Icons", StringComparison.OrdinalIgnoreCase))
            {
                // Try to get from Application Resources first
                if (Application.Current != null && Application.Current.TryGetResource("SegoeFluentIcons", null, out var resource))
                {
                    if (resource is FontFamily family)
                        return family;
                }
                
                // Fallback to direct URI - Folder based is usually better in Avalonia 11
                return SafeCreateFontFamily("avares://Flow.Launcher.Avalonia/Resources#Segoe Fluent Icons");
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static FontFamily? TryGetSystemFont(string fontNameOrPath)
    {
        try
        {
            var fontName = ExtractFontName(fontNameOrPath);
            if (string.IsNullOrEmpty(fontName))
                return null;

            return SafeCreateFontFamily(fontName);
        }
        catch
        {
            return null;
        }
    }

    private static FontFamily? SafeCreateFontFamily(string nameOrUri)
    {
        try
        {
            var family = new FontFamily(nameOrUri);
            
            // Validate if font actually exists in the system by trying to get a glyph typeface
            // This prevents returning a dummy FontFamily that will crash during rendering
            if (FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out _))
            {
                return family;
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    private static bool IsFilePath(string path)
    {
        return path.StartsWith("file:///", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
               (path.Length > 2 && path[1] == ':' && (path[2] == '\\' || path[2] == '/'));
    }

    private static string? ExtractFontName(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        // If it's a file path or URL, try to extract the fragment (after #)
        var hashIndex = path.IndexOf('#');
        if (hashIndex >= 0 && hashIndex < path.Length - 1)
        {
            return path.Substring(hashIndex + 1);
        }
        
        // If it contains slashes or backslashes, it might be a path without a fragment
        if (path.Contains('/') || path.Contains('\\'))
        {
            // Try to get the file name without extension as a fallback name
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrEmpty(fileName))
                    return fileName;
            }
            catch
            {
                // Ignore
            }
        }

        // If it's not a path-like string, assume it's just the font name
        return path;
    }

    private static FontFamily? LoadFontFromFile(string fontFamilyPath)
    {
        try
        {
            var filePath = fontFamilyPath;
            if (filePath.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                filePath = filePath.Substring(8);
            else if (filePath.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                filePath = filePath.Substring(7);
            
            var hashIndex = filePath.IndexOf('#');
            string fontFilePath;
            string? fontName;
            
            if (hashIndex >= 0)
            {
                fontFilePath = filePath.Substring(0, hashIndex);
                fontName = filePath.Substring(hashIndex + 1);
            }
            else
            {
                fontFilePath = filePath;
                fontName = null;
            }

            if (!File.Exists(fontFilePath))
                return null;

            // In Avalonia 11, for local files we should use absolute file URIs
            var uriString = $"file:///{fontFilePath.Replace('\\', '/')}";
            if (!string.IsNullOrEmpty(fontName))
                uriString += $"#{fontName}";
            
            return SafeCreateFontFamily(uriString);
        }
        catch
        {
            return null;
        }
    }
}
