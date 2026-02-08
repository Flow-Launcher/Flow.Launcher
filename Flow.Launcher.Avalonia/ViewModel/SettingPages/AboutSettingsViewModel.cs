using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Infrastructure;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class AboutSettingsViewModel : ObservableObject
{
    public string Version => Constant.Version;
    public string Website => "https://www.flowlauncher.com";
    public string GitHub => "https://github.com/Flow-Launcher/Flow.Launcher";

    [RelayCommand]
    private async Task OpenWebsite()
    {
        Process.Start(new ProcessStartInfo(Website) { UseShellExecute = true });
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenGitHub()
    {
        Process.Start(new ProcessStartInfo(GitHub) { UseShellExecute = true });
        await Task.CompletedTask;
    }
}
