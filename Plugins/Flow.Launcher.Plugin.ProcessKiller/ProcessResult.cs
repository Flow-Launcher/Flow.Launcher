using System.Diagnostics;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.Plugin.ProcessKiller
{
    internal class ProcessResult
    {
        public ProcessResult(Process process, int score, string title, MatchResult match, string tooltip)
        {
            Process = process;
            Score = score;
            Title = title;
            TitleMatch = match;
            Tooltip = tooltip;
        }

        public Process Process { get; }

        public int Score { get; }

        public string Title { get; }

        public MatchResult TitleMatch { get; }

        public string Tooltip { get; }
    }
}
