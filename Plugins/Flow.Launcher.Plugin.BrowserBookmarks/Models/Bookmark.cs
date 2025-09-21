#nullable enable
using System;

namespace Flow.Launcher.Plugin.BrowserBookmarks.Models;

public record Bookmark(string Name, string Url, string Source, string ProfilePath)
{
    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Url);
    }

    public virtual bool Equals(Bookmark? other)
    {
        return other is not null && Name == other.Name && Url == other.Url;
    }

    public string FaviconPath { get; set; } = string.Empty;
}
