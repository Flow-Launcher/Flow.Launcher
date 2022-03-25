using System;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public struct SearchResult
    {
        public string FullPath { get; init; }
        public ResultType Type { get; init; }

        public bool WindowsIndexed { get; init; }

        public bool ShowIndexState { get; init; }
    }
}