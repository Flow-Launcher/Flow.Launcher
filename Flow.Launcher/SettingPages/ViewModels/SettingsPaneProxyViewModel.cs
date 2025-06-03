using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneProxyViewModel : BaseModel
{
    public Settings Settings { get; }

    private readonly Updater _updater;

    public SettingsPaneProxyViewModel(Settings settings, Updater updater)
    {
        Settings = settings;
        _updater = updater;
    }

    [RelayCommand]
    private async Task OnTestProxyClickedAsync()
    {
        var message = await TestProxyAsync();
        App.API.ShowMsgBox(App.API.GetTranslation(message));
    }

    private async Task<string> TestProxyAsync()
    {
        if (string.IsNullOrEmpty(Settings.Proxy.Server)) return "serverCantBeEmpty";
        if (Settings.Proxy.Port <= 0) return "portCantBeEmpty";

        var handler = new HttpClientHandler
        {
            Proxy = new WebProxy(Settings.Proxy.Server, Settings.Proxy.Port)
        };

        if (!string.IsNullOrEmpty(Settings.Proxy.UserName) && !string.IsNullOrEmpty(Settings.Proxy.Password))
        {
            handler.Proxy.Credentials = new NetworkCredential(Settings.Proxy.UserName, Settings.Proxy.Password);
        }

        using var client = new HttpClient(handler);
        try
        {
            var response = await client.GetAsync(_updater.GitHubRepository);
            return response.IsSuccessStatusCode ? "proxyIsCorrect" : "proxyConnectFailed";
        }
        catch
        {
            return "proxyConnectFailed";
        }
    }
}
