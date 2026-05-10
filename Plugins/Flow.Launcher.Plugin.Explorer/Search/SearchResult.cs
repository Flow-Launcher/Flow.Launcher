using System.Collections.Generic;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public readonly record struct SearchResult
    {
        // Constructor is necesssary for record struct
        public SearchResult()
        {
        }

        public string FullPath { get; init; }
        public ResultType Type { get; init; }
        public int Score { get; init; }

        public bool WindowsIndexed { get; init; }
        public List<int> HighlightData { get; init; } = [];
    }
}
