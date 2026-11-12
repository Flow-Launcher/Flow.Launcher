using System.Threading.Tasks;
using Flow.Launcher.Plugin.Program.Programs;

namespace Flow.Launcher.Plugin.Program.ViewModels;

public partial class ProgramSettingViewModel
{
    private static partial bool SupportUWP() => UWPPackage.SupportUWP();

    private static partial Task<string> BrowseFolderAsync()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog();
        return Task.FromResult(dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null);
    }
}
