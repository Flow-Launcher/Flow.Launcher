using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.BrowserBookmark;

public partial class ChromeBookmarkLoader : ChromiumBookmarkLoader
{
    public override List<Bookmark> GetBookmarks()
    {
        return LoadChromeBookmarks();
    }

    private partial List<Bookmark> LoadChromeBookmarks();
}
