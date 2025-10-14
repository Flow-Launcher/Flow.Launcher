using System.Collections.Generic;

namespace Flow.Launcher.Plugin.Shell.ViewModels;

public class ShellSettingViewModel : BaseModel
{
    public Settings Settings { get; }

    public List<ShellLocalized> AllShells { get; } = ShellLocalized.GetValues();

    public Shell SelectedShell
    {
        get => Settings.Shell;
        set
        {
            if (Settings.Shell != value)
            {
                Settings.Shell = value;
                OnPropertyChanged();
            }
        }
    }

    public List<int> OnlyMostUsedCMDsNumbers { get; } = [5, 10, 20];
    public int SelectedOnlyMostUsedCMDsNumber
    {
        get => Settings.ShowOnlyMostUsedCMDsNumber;
        set
        {
            if (Settings.ShowOnlyMostUsedCMDsNumber != value)
            {
                Settings.ShowOnlyMostUsedCMDsNumber = value;
                OnPropertyChanged();
            }
        }
    }

    public bool CloseShellAfterPress
    {
        get => Settings.CloseShellAfterPress;
        set
        {
            if (Settings.CloseShellAfterPress != value)
            {
                Settings.CloseShellAfterPress = value;
                OnPropertyChanged();
                // Only allow CloseShellAfterPress to be true when LeaveShellOpen is false
                if (value)
                {
                    LeaveShellOpen = false;
                }
            }
        }
    }

    public bool LeaveShellOpen
    {
        get => Settings.LeaveShellOpen;
        set
        {
            if (Settings.LeaveShellOpen != value)
            {
                Settings.LeaveShellOpen = value;
                OnPropertyChanged();
                // Only allow LeaveShellOpen to be true when CloseShellAfterPress is false
                if (value)
                {
                    CloseShellAfterPress = false;
                }
            }
        }
    }

    public ShellSettingViewModel(Settings settings)
    {
        Settings = settings;
    }
}
