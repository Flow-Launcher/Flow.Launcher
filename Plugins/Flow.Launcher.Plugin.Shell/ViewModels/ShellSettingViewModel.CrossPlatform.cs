using System.Collections.Generic;
using System.IO;

namespace Flow.Launcher.Plugin.Shell.ViewModels;

public partial class ShellSettingViewModel
{
    /// <summary>
    /// Replace Win+R, Run as administrator and Windows Terminal settings.
    /// </summary>
    public bool ShowWindowsOnlySettings => false;

    /// <summary>
    /// cmd, PowerShell and pwsh are Windows shells. Here the managed shell (stored as <see cref="Shell.Cmd"/>,
    /// the default setting) is the user's login shell, so it is listed under that shell's name.
    /// </summary>
    private static partial List<ShellLocalized> GetAvailableShells()
    {
        var shells = ShellLocalized.GetValues();
        shells.RemoveAll(s => s.Value is Shell.Powershell or Shell.Pwsh);
        var userShell = shells.Find(s => s.Value == Shell.Cmd);
        userShell.Display = Path.GetFileName(Main.GetUserShell());
        return shells;
    }
}
