using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.BrowserBookmark
{
    public class EdgeBookmarkLoader : ChromiumBookmarkLoader
    {

        private readonly List<Bookmark> _bookmarks = new();

        private void LoadEdgeBookmarks()
        {
            var platformPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            LoadBookmarks(Path.Combine(platformPath, @"Microsoft\Edge\User Data"), "Microsoft Edge");
            LoadBookmarks(Path.Combine(platformPath, @"Microsoft\Edge Dev\User Data"), "Microsoft Edge Dev");
            LoadBookmarks(Path.Combine(platformPath, @"Microsoft\Edge SxS\User Data"), "Microsoft Edge Canary");
        }
        
        public override List<Bookmark> GetBookmarks()
        {
            _bookmarks.Clear();
            LoadEdgeBookmarks();
            return _bookmarks;
        }
    }
}