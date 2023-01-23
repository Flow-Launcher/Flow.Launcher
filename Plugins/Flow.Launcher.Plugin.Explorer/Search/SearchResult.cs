using System;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public record struct SearchResult
    {
        public string Name { get; init; }
        public string FullPath { get; init; }
        public ResultType Type { get; init; }
        public int Score { get; init; }

        public bool WindowsIndexed { get; init; }
    }
}
