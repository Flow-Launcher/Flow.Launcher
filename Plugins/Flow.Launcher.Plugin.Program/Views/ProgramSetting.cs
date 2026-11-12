using System.Collections.Generic;
using Flow.Launcher.Plugin.Program.Views.Models;

namespace Flow.Launcher.Plugin.Program.Views
{
    // Shared by the WPF settings page (ProgramSetting.xaml.cs) and the Avalonia settings view model
    public partial class ProgramSetting
    {
        // We do not save all program sources to settings, so using
        // this as temporary holder for displaying all loaded programs sources.
        internal static List<ProgramSource> ProgramSettingDisplayList { get; set; }
    }
}
