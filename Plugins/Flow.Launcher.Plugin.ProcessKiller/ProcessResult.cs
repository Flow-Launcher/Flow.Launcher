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

        public ProcessResult(Process process, int score, int port)
        {
            Process = process;
            Score = score;
            Port = port;
        }

        public Process Process { get; }

        public int Score { get; }

        public int Port { set; get; } = 0;
    }
}