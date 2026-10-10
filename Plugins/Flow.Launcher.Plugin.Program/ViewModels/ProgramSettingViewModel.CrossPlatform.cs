using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Flow.Launcher.Plugin.Program.ViewModels;

public partial class ProgramSettingViewModel
{
    // UWP packages only exist on Windows
    private static partial bool SupportUWP() => false;

    private static async partial Task<string> BrowseFolderAsync()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        var topLevel = TopLevel.GetTopLevel(desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow);
        if (topLevel == null)
            return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }
}
