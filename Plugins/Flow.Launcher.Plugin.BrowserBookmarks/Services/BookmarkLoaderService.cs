#nullable enable
using Flow.Launcher.Plugin.BrowserBookmarks.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.BrowserBookmarks.Services;

public class BookmarkLoaderService
{
    private readonly PluginInitContext _context;
    private readonly Settings _settings;
    private readonly string _tempPath;
    
    // This will hold the actual paths to the bookmark files for the watcher service
    public List<string> DiscoveredBookmarkFiles { get; } = new();

    public BookmarkLoaderService(PluginInitContext context, Settings settings, string tempPath)
    {
        _context = context;
        _settings = settings;
        _tempPath = tempPath;
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
                _context.API.LogException(nameof(BookmarkLoaderService), $"Failed to load bookmarks from {loader.Name}.", e);
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

        if (_settings.LoadChromiumBookmark)
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Chromium\User Data");
            if(Directory.Exists(path))
                yield return new ChromiumBookmarkLoader("Chromium", path, logAction, DiscoveredBookmarkFiles);
        }

        if (_settings.LoadFirefoxBookmark)
        {
            string? placesPath = null;
            try
            {
                placesPath = FirefoxProfileFinder.GetFirefoxPlacesPath();
            }
            catch (Exception ex)
            {
                _context.API.LogException(nameof(BookmarkLoaderService), "Failed to find Firefox profile", ex);
            }
            if (!string.IsNullOrEmpty(placesPath))
            {
                yield return new FirefoxBookmarkLoader("Firefox", placesPath, _tempPath, logAction);
            }
            
            string? msixPlacesPath = null;
            try
            {
                msixPlacesPath = FirefoxProfileFinder.GetFirefoxMsixPlacesPath();
            }
            catch (Exception ex)
            {
                _context.API.LogException(nameof(BookmarkLoaderService), "Failed to find Firefox MSIX package", ex);
            }
            if (!string.IsNullOrEmpty(msixPlacesPath))
            {
                yield return new FirefoxBookmarkLoader("Firefox (Store)", msixPlacesPath, _tempPath, logAction);
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
        var placesPath = FirefoxProfileFinder.GetPlacesPathFromProfileDir(dataDirectoryPath);
        if (string.IsNullOrEmpty(placesPath))
        {
            // Or they might point directly to a profile folder (e.g. ...\Profiles\xyz.default-release)
            placesPath = Path.Combine(dataDirectoryPath, "places.sqlite");
        }

        // Do not add Firefox places.sqlite to the watcher as it's updated constantly for history.
        return new FirefoxBookmarkLoader(name, placesPath, _tempPath, logAction);
    }
}
