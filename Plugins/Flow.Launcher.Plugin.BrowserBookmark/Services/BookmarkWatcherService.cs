using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
namespace Flow.Launcher.Plugin.BrowserBookmark.Services;

public class BookmarkWatcherService : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = [];
    public event Action OnBookmarkFileChanged;

    // Timer to debounce file change events
    private Timer _debounceTimer;
    private readonly object _lock = new();
    private volatile bool _disposed;

    public BookmarkWatcherService()
    {
        _debounceTimer = new Timer(_ => OnBookmarkFileChanged?.Invoke(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void UpdateWatchers(IEnumerable<string> filePaths)
    {
        // Dispose old watchers
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();

        // Create a new, specific watcher for each individual bookmark file.
        foreach (var filePath in filePaths)
        {
            var directory = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName) || !Directory.Exists(directory))
                continue;

            var watcher = new FileSystemWatcher(directory)
            {
                Filter = fileName,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };
            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileChanged;
            watcher.Deleted += OnFileChanged;
            watcher.Renamed += OnFileRenamed;
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        TriggerDebouncedReload();
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        TriggerDebouncedReload();
    }

    private void TriggerDebouncedReload()
    {
        // Reset the timer to fire after 2 seconds.
        // This prevents multiple reloads if a browser writes to the file several times in quick succession.
        lock (_lock)
        {
            if (_disposed) return;
            _debounceTimer?.Change(2000, Timeout.Infinite);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            _debounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _debounceTimer?.Dispose();
        }
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnFileChanged;
            watcher.Created -= OnFileChanged;
            watcher.Deleted -= OnFileChanged;
            watcher.Renamed -= OnFileRenamed;
            watcher.Dispose();
        }
    }
}
