using Flow.Launcher.Plugin.Everything.Everything;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    public record struct EverythingSearchOption(
        string Keyword,
        SortOption SortOption,
        bool IsContentSearch = false,
        string ContentSearchKeyword = default,
        string ParentPath = default,
        bool IsRecursive = true,
        int Offset = 0,
        int MaxCount = 100,
        bool IsFullPathSearch = true,
        bool IsRunCounterEnabled = true
    );
}
