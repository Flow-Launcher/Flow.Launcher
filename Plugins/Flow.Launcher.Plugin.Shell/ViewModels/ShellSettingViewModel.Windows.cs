using System.Collections.Generic;

namespace Flow.Launcher.Plugin.Shell.ViewModels;

public partial class ShellSettingViewModel
{
    /// <summary>
    /// Replace Win+R, Run as administrator and Windows Terminal settings.
    /// </summary>
    public bool ShowWindowsOnlySettings => true;

    private static partial List<ShellLocalized> GetAvailableShells() => ShellLocalized.GetValues();
}
