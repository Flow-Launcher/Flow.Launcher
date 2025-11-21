using System.Collections.Generic;
using System.Threading;
using Flow.Launcher.Plugin.BrowserBookmark.Models;

namespace Flow.Launcher.Plugin.BrowserBookmark.Services;

public interface IBookmarkLoader
{
    string Name { get; }

    IAsyncEnumerable<Bookmark> GetBookmarksAsync(CancellationToken cancellationToken = default);
}
