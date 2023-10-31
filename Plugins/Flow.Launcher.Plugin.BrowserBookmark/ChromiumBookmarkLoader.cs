using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Flow.Launcher.Plugin.BrowserBookmark
{
    public abstract class ChromiumBookmarkLoader : IBookmarkLoader
    {
        public abstract List<Bookmark> GetBookmarks();
        protected List<Bookmark> LoadBookmarks(string browserDataPath, string name)
        {
            var bookmarks = new List<Bookmark>();
            if (!Directory.Exists(browserDataPath)) return bookmarks;
            var paths = Directory.GetDirectories(browserDataPath);

            foreach (var profile in paths)
            {
                var bookmarkPath = Path.Combine(profile, "Bookmarks");
                if (!File.Exists(bookmarkPath))
                    continue;
                
                Main.RegisterBookmarkFile(bookmarkPath);

                var source = name + (Path.GetFileName(profile) == "Default" ? "" : $" ({Path.GetFileName(profile)})");
                bookmarks.AddRange(LoadBookmarksFromFile(bookmarkPath, source));
            }
            return bookmarks;
        }

        protected List<Bookmark> LoadBookmarksFromFile(string path, string source)
        {
            if (!File.Exists(path))
                return new();

            var bookmarks = new List<Bookmark>();
            using var jsonDocument = JsonDocument.Parse(File.ReadAllText(path));
            if (!jsonDocument.RootElement.TryGetProperty("roots", out var rootElement))
                return new();
            foreach (var folder in rootElement.EnumerateObject())
            {
                if (folder.Value.ValueKind == JsonValueKind.Object)
                    EnumerateFolderBookmark(folder.Value, bookmarks, source);
            }
            return bookmarks;
        }

        private void EnumerateFolderBookmark(JsonElement folderElement, List<Bookmark> bookmarks, string source)
        {
            if (!folderElement.TryGetProperty("children", out var childrenElement))
                return;
            foreach (var subElement in childrenElement.EnumerateArray())
            {
                switch (subElement.GetProperty("type").GetString())
                {
                    case "folder":
                    case "workspace": // Edge Workspace
                        EnumerateFolderBookmark(subElement, bookmarks, source);
                        break;
                    default:
                        bookmarks.Add(new Bookmark(
                            subElement.GetProperty("name").GetString(),
                            subElement.GetProperty("url").GetString(),
                            source));
                        break;
                }
            }

        }
    }
}
