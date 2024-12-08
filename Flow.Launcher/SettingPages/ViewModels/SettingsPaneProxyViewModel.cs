using System.Net;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneProxyViewModel : BaseModel
{
    private readonly Updater _updater;
    public Settings Settings { get; }

    public SettingsPaneProxyViewModel(Settings settings, Updater updater)
    {
        _updater = updater;
        Settings = settings;
    }

    [RelayCommand]
    private void OnTestProxyClicked()
    {
        var message = TestProxy();
        MessageBoxEx.Show(InternationalizationManager.Instance.GetTranslation(message));
    }

    private string TestProxy()
    {
        if (string.IsNullOrEmpty(Settings.Proxy.Server)) return "serverCantBeEmpty";
        if (Settings.Proxy.Port <= 0) return "portCantBeEmpty";

        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_updater.GitHubRepository);

        if (string.IsNullOrEmpty(Settings.Proxy.UserName) || string.IsNullOrEmpty(Settings.Proxy.Password))
        {
            request.Proxy = new WebProxy(Settings.Proxy.Server, Settings.Proxy.Port);
        }
        else
        {
            request.Proxy = new WebProxy(Settings.Proxy.Server, Settings.Proxy.Port)
            {
                Credentials = new NetworkCredential(Settings.Proxy.UserName, Settings.Proxy.Password)
            };
        }

        try
        {
            var response = (HttpWebResponse)request.GetResponse();
            return response.StatusCode switch
            {
                HttpStatusCode.OK => "proxyIsCorrect",
                _ => "proxyConnectFailed"
            };
        }
        catch
        {
            return "proxyConnectFailed";
        }
    }
}
