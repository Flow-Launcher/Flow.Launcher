using System.Collections.Generic;

namespace Flow.Launcher.Plugin.Shell
{
    public class Settings
    {
        public Shell Shell { get; set; } = Shell.Cmd;
        
        public bool ReplaceWinR { get; set; } = false;
        
        public bool LeaveShellOpen { get; set; }

        public bool RunAsAdministrator { get; set; } = true;

        public bool ShowOnlyMostUsedCMDs { get; set; }

        public int ShowOnlyMostUsedCMDsNumber { get; set; }

        public Dictionary<string, int> CommandHistory { get; set; } = new Dictionary<string, int>();

        public void AddCmdHistory(string cmdName)
        {
            if (CommandHistory.ContainsKey(cmdName))
            {
                CommandHistory[cmdName] += 1;
            }
            else
            {
                CommandHistory.Add(cmdName, 1);
            }
        }
    }

    public enum Shell
    {
        Cmd = 0,
        Powershell = 1,
        RunCommand = 2,

    }
}
