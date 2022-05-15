using Flow.Launcher.Plugin.Everything.Everything;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    public struct EverythingSearchOption
    {
        public EverythingSearchOption(string keyword, 
            SortOption sortOption,
            bool isContentSearch = false, 
            string contentSearchKeyword = "",
            string parentPath = "",
            bool isRecursive = true,
            int offset = 0, 
            int maxCount = 100)
        {
            Keyword = keyword;
            SortOption = sortOption;
            ContentSearchKeyword = contentSearchKeyword;
            IsContentSearch = isContentSearch;
            ParentPath = parentPath;
            IsRecursive = isRecursive;
            Offset = offset;
            MaxCount = maxCount;
        }
        public string Keyword { get; set; }
        public SortOption SortOption { get; set; }
        public string ParentPath { get; set; }
        public bool IsRecursive { get; set; }
        
        public bool IsContentSearch { get; set; }
        public string ContentSearchKeyword { get; set; }
        public int Offset { get;set; }
        public int MaxCount { get; set; }
    }
}