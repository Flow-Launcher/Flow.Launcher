using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.BrowserBookmark;

public interface IBookmarkLoader
{
    public List<Bookmark> GetBookmarks();
}
