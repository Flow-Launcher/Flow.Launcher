using System.Collections.Generic;

namespace Flow.Launcher.Plugin.BrowserBookmark
{
    public interface IBookmarkLoader
    {
        public List<Bookmark> GetBookmarks();
    }
}