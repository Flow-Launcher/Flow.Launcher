using System.Collections.Generic;
using Flow.Launcher.Localization.Attributes;

namespace Flow.Launcher.Plugin.Shell
{
    public class Settings : BaseModel
    {
        private Shell _shell = Shell.Cmd;
        public Shell Shell
        {
            get => _shell;
            set
            {
                if (_shell != value)
                {
                    _shell = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _replaceWinR = false;
        public bool ReplaceWinR
        {
            get => _replaceWinR;
            set
            {
                if (_replaceWinR != value)
                {
                    _replaceWinR = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _closeShellAfterPress = false;
        public bool CloseShellAfterPress
        {
            get => _closeShellAfterPress;
            set
            {
                if (_closeShellAfterPress != value)
                {
                    _closeShellAfterPress = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _leaveShellOpen;
        public bool LeaveShellOpen
        {
            get => _leaveShellOpen;
            set
            {
                if (_leaveShellOpen != value)
                {
                    _leaveShellOpen = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _runAsAdministrator = true;
        public bool RunAsAdministrator
        {
            get => _runAsAdministrator;
            set
            {
                if (_runAsAdministrator != value)
                {
                    _runAsAdministrator = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _useWindowsTerminal = false;
        public bool UseWindowsTerminal
        {
            get => _useWindowsTerminal;
            set
            {
                if (_useWindowsTerminal != value)
                {
                    _useWindowsTerminal = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _showOnlyMostUsedCMDs;
        public bool ShowOnlyMostUsedCMDs
        {
            get => _showOnlyMostUsedCMDs;
            set
            {
                if (_showOnlyMostUsedCMDs != value)
                {
                    _showOnlyMostUsedCMDs = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _showOnlyMostUsedCMDsNumber;
        public int ShowOnlyMostUsedCMDsNumber
        {
            get => _showOnlyMostUsedCMDsNumber;
            set
            {
                if (_showOnlyMostUsedCMDsNumber != value)
                {
                    _showOnlyMostUsedCMDsNumber = value;
                    OnPropertyChanged();
                }
            }
        }

        public Dictionary<string, int> CommandHistory { get; set; } = [];

        public void AddCmdHistory(string cmdName)
        {
            if (!CommandHistory.TryAdd(cmdName, 1))
            {
                CommandHistory[cmdName] += 1;
            }
        }
    }

    [EnumLocalize]
    public enum Shell
    {
        [EnumLocalizeValue("CMD")]
        Cmd = 0,

        [EnumLocalizeValue("PowerShell")]
        Powershell = 1,

        [EnumLocalizeValue("RunCommand")]
        RunCommand = 2,

        [EnumLocalizeValue("Pwsh")]
        Pwsh = 3,
    }
}
