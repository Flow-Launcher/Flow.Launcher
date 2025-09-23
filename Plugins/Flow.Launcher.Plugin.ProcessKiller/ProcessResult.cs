using System.Diagnostics;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.Plugin.ProcessKiller;

internal class ProcessResult(Process process, int score, string title, MatchResult match, string tooltip)
{
    public Process Process { get; } = process;

    public int Score { get; } = score;

    public string Title { get; } = title;

    public MatchResult TitleMatch { get; } = match;

    public string Tooltip { get; } = tooltip;
}
