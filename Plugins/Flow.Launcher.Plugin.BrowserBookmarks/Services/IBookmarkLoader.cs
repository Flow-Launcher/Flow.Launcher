using Flow.Launcher.Plugin.BrowserBookmarks.Models;
using System.Collections.Generic;
using System.Threading;

namespace Flow.Launcher.Plugin.BrowserBookmarks.Services;

public interface IBookmarkLoader
{
    IAsyncEnumerable<Bookmark> GetBookmarksAsync(CancellationToken cancellationToken = default);
}