using System.Diagnostics;

namespace Flow.Launcher.Plugin.ProcessKiller
{
    internal class ProcessResult
    {
        public ProcessResult(Process process, int score)
        {
            Process = process;
            Score = score;
        }

        public Process Process { get; }

        public int Score { get; }
    }
}