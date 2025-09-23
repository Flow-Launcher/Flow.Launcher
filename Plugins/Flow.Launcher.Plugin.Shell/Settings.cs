using System.Collections.Generic;

namespace Flow.Launcher.Plugin.Shell
{
    public class Settings
    {
        public Shell Shell { get; set; } = Shell.Cmd;

        public bool ReplaceWinR { get; set; } = false;

        public bool CloseShellAfterPress { get; set; } = false;

        public bool LeaveShellOpen { get; set; }

        public bool RunAsAdministrator { get; set; } = true;

        public bool UseWindowsTerminal { get; set; } = false;

        public bool ShowOnlyMostUsedCMDs { get; set; }

        public int ShowOnlyMostUsedCMDsNumber { get; set; }

        public Dictionary<string, int> CommandHistory { get; set; } = [];

        public void AddCmdHistory(string cmdName)
        {
            if (!CommandHistory.TryAdd(cmdName, 1))
            {
                CommandHistory[cmdName] += 1;
            }
        }
    }

    public enum Shell
    {
        Cmd = 0,
        Powershell = 1,
        RunCommand = 2,
        Pwsh = 3,
    }
}
