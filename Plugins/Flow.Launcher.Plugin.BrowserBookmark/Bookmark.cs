using BinaryAnalysis.UnidecodeSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace Flow.Launcher.Plugin.BrowserBookmark
{
    // Source may be important in the future
    public record Bookmark(string Name, string Url, string Source = "")
    {
        public override int GetHashCode()
        {
            var hashName = Name?.GetHashCode() ?? 0;
            var hashUrl = Url?.GetHashCode() ?? 0;
            return hashName ^ hashUrl;
        }

        public virtual bool Equals(Bookmark other)
        {
            return other != null && Name == other.Name && Url == other.Url;
        }
    }
}