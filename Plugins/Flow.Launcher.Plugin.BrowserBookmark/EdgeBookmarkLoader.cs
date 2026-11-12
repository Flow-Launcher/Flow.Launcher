using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.BrowserBookmark;

public partial class EdgeBookmarkLoader : ChromiumBookmarkLoader
{
    private partial List<Bookmark> LoadEdgeBookmarks();

    public override List<Bookmark> GetBookmarks() => LoadEdgeBookmarks();
}
