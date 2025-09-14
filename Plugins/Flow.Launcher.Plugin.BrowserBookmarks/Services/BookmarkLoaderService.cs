using Flow.Launcher.Plugin.BrowserBookmarks.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.BrowserBookmarks.Services;

public class BookmarkLoaderService
{
    private readonly PluginInitContext _context;
    private readonly Settings _settings;
    
    // This will hold the actual paths to the bookmark files for the watcher service
    public List<string> DiscoveredBookmarkFiles { get; } = new();

    public BookmarkLoaderService(PluginInitContext context, Settings settings)
    {
        _context = context;
        _settings = settings;
    }

    public async Task<List<Bookmark>> LoadBookmarksAsync(CancellationToken cancellationToken)
    {
        DiscoveredBookmarkFiles.Clear();
        var bookmarks = new ConcurrentBag<Bookmark>();
        var loaders = GetBookmarkLoaders();

        var tasks = loaders.Select(async loader =>
        {
            try
            {
                await foreach (var bookmark in loader.GetBookmarksAsync(cancellationToken).WithCancellation(cancellationToken))
                {
                    bookmarks.Add(bookmark);
                }
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled, swallow exception
            }
            catch (Exception e)
            {
                _context.API.LogException(nameof(BookmarkLoaderService), $"Failed to load bookmarks from a source.", e);
            }
        });

        await Task.WhenAll(tasks);

        return bookmarks.Distinct().ToList();
    }

    private IEnumerable<IBookmarkLoader> GetBookmarkLoaders()
    {
        var logAction = (string tag, string msg, Exception? ex) => _context.API.LogException(tag, msg, ex);

        if (_settings.LoadChromeBookmark)
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data");
            if(Directory.Exists(path))
                yield return new ChromiumBookmarkLoader("Google Chrome", path, logAction, DiscoveredBookmarkFiles);
        }

        if (_settings.LoadEdgeBookmark)
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data");
            if(Directory.Exists(path))
                yield return new ChromiumBookmarkLoader("Microsoft Edge", path, logAction, DiscoveredBookmarkFiles);
        }

        if (_settings.LoadFirefoxBookmark)
        {
            // Standard MSI installer path
            var placesPath = GetFirefoxPlacesPathFromProfileDir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Mozilla\Firefox"));
            if (!string.IsNullOrEmpty(placesPath))
            {
                // Do not add Firefox places.sqlite to the watcher as it's updated constantly for history.
                yield return new FirefoxBookmarkLoader("Firefox", placesPath, _context.CurrentPluginMetadata.PluginCacheDirectoryPath, logAction);
            }
            
            // MSIX (Microsoft Store) installer path
            var msixPlacesPath = GetFirefoxMsixPlacesPath();
            if (!string.IsNullOrEmpty(msixPlacesPath))
            {
                // Do not add Firefox places.sqlite to the watcher as it's updated constantly for history.
                yield return new FirefoxBookmarkLoader("Firefox (Store)", msixPlacesPath, _context.CurrentPluginMetadata.PluginCacheDirectoryPath, logAction);
            }
        }

        foreach (var browser in _settings.CustomBrowsers)
        {
            if (string.IsNullOrEmpty(browser.Name) || string.IsNullOrEmpty(browser.DataDirectoryPath) || !Directory.Exists(browser.DataDirectoryPath))
                continue;

            IBookmarkLoader loader = browser.BrowserType switch
            {
                BrowserType.Chromium => new ChromiumBookmarkLoader(browser.Name, browser.DataDirectoryPath, logAction, DiscoveredBookmarkFiles),
                BrowserType.Firefox => CreateCustomFirefoxLoader(browser.Name, browser.DataDirectoryPath),
                _ => new ChromiumBookmarkLoader(browser.Name, browser.DataDirectoryPath, logAction, DiscoveredBookmarkFiles)
            };
            yield return loader;
        }
    }
    
    private IBookmarkLoader CreateCustomFirefoxLoader(string name, string dataDirectoryPath)
    {
        var logAction = (string tag, string msg, Exception? ex) => _context.API.LogException(tag, msg, ex);
        // Custom Firefox paths might point to the root profile dir (e.g. ...\Mozilla\Firefox)
        var placesPath = GetFirefoxPlacesPathFromProfileDir(dataDirectoryPath);
        if (string.IsNullOrEmpty(placesPath))
        {
            // Or they might point directly to a profile folder (e.g. ...\Profiles\xyz.default-release)
            placesPath = Path.Combine(dataDirectoryPath, "places.sqlite");
        }
        
        // Do not add Firefox places.sqlite to the watcher as it's updated constantly for history.
        return new FirefoxBookmarkLoader(name, placesPath, _context.CurrentPluginMetadata.PluginCacheDirectoryPath, logAction);
    }
    
    private string GetFirefoxMsixPlacesPath()
    {
        var packagesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
        if (!Directory.Exists(packagesPath)) return string.Empty;

        try
        {
            var firefoxPackageFolder = Directory.EnumerateDirectories(packagesPath, "Mozilla.Firefox*", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (firefoxPackageFolder == null) return string.Empty;

            var profileFolderPath = Path.Combine(firefoxPackageFolder, @"LocalCache\Roaming\Mozilla\Firefox");
            return GetFirefoxPlacesPathFromProfileDir(profileFolderPath);
        }
        catch (Exception ex)
        {
            _context.API.LogException(nameof(BookmarkLoaderService), "Failed to find Firefox MSIX package", ex);
            return string.Empty;
        }
    }

    private static string GetFirefoxPlacesPathFromProfileDir(string profileFolderPath)
    {
        var profileIni = Path.Combine(profileFolderPath, @"profiles.ini");
        if (!File.Exists(profileIni))
            return string.Empty;

        try
        {
            var iniContent = File.ReadAllText(profileIni);
            var profileSectionMatch = Regex.Match(iniContent, @"\[Profile[^\]]+\]\s*Name=default-release[\s\S]+?Path=([^\r\n]+)[\s\S]+?Default=1", RegexOptions.IgnoreCase);
            if (!profileSectionMatch.Success)
            {
                profileSectionMatch = Regex.Match(iniContent, @"\[Profile[^\]]+\][\s\S]+?Path=([^\r\n]+)[\s\S]+?Default=1", RegexOptions.IgnoreCase);
            }
            if (!profileSectionMatch.Success)
            {
                profileSectionMatch = Regex.Match(iniContent, @"\[Profile[^\]]+\][\s\S]+?Path=([^\r\n]+)");
            }
            if (!profileSectionMatch.Success) return string.Empty;

            var path = profileSectionMatch.Groups[1].Value;
            var isRelative = !path.Contains(':');

            var profilePath = isRelative ? Path.Combine(profileFolderPath, path.Replace('/', Path.DirectorySeparatorChar)) : path;
            var placesDb = Path.Combine(profilePath, "places.sqlite");

            return File.Exists(placesDb) ? placesDb : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
