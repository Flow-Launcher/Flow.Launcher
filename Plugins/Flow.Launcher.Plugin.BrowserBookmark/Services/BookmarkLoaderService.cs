#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.BrowserBookmark.Models;

namespace Flow.Launcher.Plugin.BrowserBookmark.Services;

public class BookmarkLoaderService
{
    private readonly PluginInitContext _context;
    private readonly Settings _settings;
    private readonly string _tempPath;

    public BookmarkLoaderService(PluginInitContext context, Settings settings, string tempPath)
    {
        _context = context;
        _settings = settings;
        _tempPath = tempPath;
    }

    public async Task<(List<Bookmark> Bookmarks, List<string> DiscoveredFiles)> LoadBookmarksAsync(CancellationToken cancellationToken)
    {
        var discoveredBookmarkFiles = new ConcurrentBag<string>();
        var bookmarks = new ConcurrentBag<Bookmark>();
        var loaders = GetBookmarkLoaders(discoveredBookmarkFiles);

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

        return (bookmarks.Distinct().ToList(), discoveredBookmarkFiles.Distinct().ToList());
    }

    public IEnumerable<IBookmarkLoader> GetBookmarkLoaders(ConcurrentBag<string> discoveredBookmarkFiles)
    {
        return GetChromiumBookmarkLoaders(discoveredBookmarkFiles).Concat(GetFirefoxBookmarkLoaders());
    }

    public IEnumerable<IBookmarkLoader> GetChromiumBookmarkLoaders(ConcurrentBag<string> discoveredBookmarkFiles)
    {
        var logAction = (string tag, string msg, Exception? ex) => _context.API.LogException(tag, msg, ex);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (_settings.LoadChromeBookmark)
        {
            var path = Path.Combine(localAppData, @"Google\Chrome\User Data");
            if (Directory.Exists(path))
                yield return new ChromiumBookmarkLoader("Google Chrome", path, logAction, discoveredBookmarkFiles);

            var canaryPath = Path.Combine(localAppData, @"Google\Chrome SxS\User Data");
            if (Directory.Exists(canaryPath))
                yield return new ChromiumBookmarkLoader("Google Chrome Canary", canaryPath, logAction, discoveredBookmarkFiles);
        }

        if (_settings.LoadEdgeBookmark)
        {
            var path = Path.Combine(localAppData, @"Microsoft\Edge\User Data");
            if (Directory.Exists(path))
                yield return new ChromiumBookmarkLoader("Microsoft Edge", path, logAction, discoveredBookmarkFiles);

            var devPath = Path.Combine(localAppData, @"Microsoft\Edge Dev\User Data");
            if (Directory.Exists(devPath))
                yield return new ChromiumBookmarkLoader("Microsoft Edge Dev", devPath, logAction, discoveredBookmarkFiles);

            var canaryPath = Path.Combine(localAppData, @"Microsoft\Edge SxS\User Data");
            if (Directory.Exists(canaryPath))
                yield return new ChromiumBookmarkLoader("Microsoft Edge Canary", canaryPath, logAction, discoveredBookmarkFiles);
        }

        if (_settings.LoadChromiumBookmark)
        {
            var path = Path.Combine(localAppData, @"Chromium\User Data");
            if (Directory.Exists(path))
                yield return new ChromiumBookmarkLoader("Chromium", path, logAction, discoveredBookmarkFiles);
        }

        foreach (var browser in _settings.CustomBrowsers.Where(b => b.BrowserType == BrowserType.Chromium))
        {
            if (string.IsNullOrEmpty(browser.Name) || string.IsNullOrEmpty(browser.DataDirectoryPath) || !Directory.Exists(browser.DataDirectoryPath))
                continue;

            yield return new ChromiumBookmarkLoader(browser.Name, browser.DataDirectoryPath, logAction, discoveredBookmarkFiles);
        }
    }

    public IEnumerable<IBookmarkLoader> GetFirefoxBookmarkLoaders()
    {
        var logAction = (string tag, string msg, Exception? ex) => _context.API.LogException(tag, msg, ex);

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

        foreach (var browser in _settings.CustomBrowsers.Where(b => b.BrowserType == BrowserType.Firefox))
        {
            if (string.IsNullOrEmpty(browser.Name) || string.IsNullOrEmpty(browser.DataDirectoryPath) || !Directory.Exists(browser.DataDirectoryPath))
                continue;

            yield return CreateCustomFirefoxLoader(browser.Name, browser.DataDirectoryPath);
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
