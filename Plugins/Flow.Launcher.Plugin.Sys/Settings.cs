using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.Sys;

public class Settings : BaseModel
{
    public Settings()
    {
        if (Commands.Count > 0)
        {
            SelectedCommand = Commands[0];
        }
    }

    public ObservableCollection<Command> Commands { get; set; } = new ObservableCollection<Command>
    {
        new()
        {
            Key = "Shutdown",
            Keyword = "Shutdown"
        },
        new()
        {
            Key = "Restart",
            Keyword = "Restart"
        },
        new()
        {
            Key = "Restart With Advanced Boot Options",
            Keyword = "Restart With Advanced Boot Options"
        },
        new()
        {
            Key = "Log Off/Sign Out",
            Keyword = "Log Off/Sign Out"
        },
        new()
        {
            Key = "Lock",
            Keyword = "Lock"
        },
        new()
        {
            Key = "Sleep",
            Keyword = "Sleep"
        },
        new()
        {
            Key = "Hibernate",
            Keyword = "Hibernate"
        },
        new()
        {
            Key = "Index Option",
            Keyword = "Index Option"
        },
        new()
        {
            Key = "Empty Recycle Bin",
            Keyword = "Empty Recycle Bin"
        },
        new()
        {
            Key = "Open Recycle Bin",
            Keyword = "Open Recycle Bin"
        },
        new()
        {
            Key = "Exit",
            Keyword = "Exit"
        },
        new()
        {
            Key = "Save Settings",
            Keyword = "Save Settings"
        },
        new()
        {
            Key = "Restart Flow Launcher",
            Keyword = "Restart Flow Launcher"
        },
        new()
        {
            Key = "Settings",
            Keyword = "Settings"
        },
        new()
        {
            Key = "Reload Plugin Data",
            Keyword = "Reload Plugin Data"
        },
        new()
        {
            Key = "Check For Update",
            Keyword = "Check For Update"
        },
        new()
        {
            Key = "Open Log Location",
            Keyword = "Open Log Location"
        },
        new()
        {
            Key = "Flow Launcher Tips",
            Keyword = "Flow Launcher Tips"
        },
        new()
        {
            Key = "Flow Launcher UserData Folder",
            Keyword = "Flow Launcher UserData Folder"
        },
        new()
        {
            Key = "Toggle Game Mode",
            Keyword = "Toggle Game Mode"
        },
        new()
        {
            Key = "Set Flow Launcher Theme",
            Keyword = "Set Flow Launcher Theme"
        }
    };

    [JsonIgnore]
    public Command SelectedCommand { get; set; }
}
