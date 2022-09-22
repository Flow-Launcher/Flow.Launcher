using System;
using Flow.Launcher.Plugin.Everything.Everything;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    public record struct EverythingSearchOption(ReadOnlySpan<char> Keyword, 
        SortOption SortOption,
        bool IsContentSearch = false, 
        ReadOnlySpan<char> ContentSearchKeyword = default,
        ReadOnlySpan<char> ParentPath = default,
        bool IsRecursive = true,
        int Offset = 0, 
        int MaxCount = 100);
}
