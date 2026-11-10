using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class ProxySettingsViewModel : ObservableObject
{
    private readonly Settings _settings;

    public ProxySettingsViewModel()
    {
        _settings = Ioc.Default.GetRequiredService<Settings>();
    }

    [RelayCommand]
    private async Task TestProxyAsync()
    {
        var messageKey = await CheckProxyAsync();
        App.API.ShowMsgBox(App.API.GetTranslation(messageKey));
    }

    // Returns the translation key describing the result, like WPF SettingsPaneProxyViewModel.TestProxyAsync.
    private async Task<string> CheckProxyAsync()
    {
        var proxy = _settings.Proxy;
        if (string.IsNullOrEmpty(proxy.Server)) return "serverCantBeEmpty";
        if (proxy.Port <= 0) return "portCantBeEmpty";

        var handler = new HttpClientHandler
        {
            Proxy = new WebProxy(proxy.Server, proxy.Port)
        };

        if (!string.IsNullOrEmpty(proxy.UserName) && !string.IsNullOrEmpty(proxy.Password))
        {
            handler.Proxy.Credentials = new NetworkCredential(proxy.UserName, proxy.Password);
        }

        using var client = new HttpClient(handler);
        try
        {
            var response = await client.GetAsync(Constant.GitHub);
            return response.IsSuccessStatusCode ? "proxyIsCorrect" : "proxyConnectFailed";
        }
        catch
        {
            return "proxyConnectFailed";
        }
    }

    public bool ProxyEnabled
    {
        get => _settings.Proxy.Enabled;
        set
        {
            if (_settings.Proxy.Enabled != value)
            {
                _settings.Proxy.Enabled = value;
                OnPropertyChanged();
            }
        }
    }

    public string ProxyServer
    {
        get => _settings.Proxy.Server;
        set
        {
            if (_settings.Proxy.Server != value)
            {
                _settings.Proxy.Server = value;
                OnPropertyChanged();
            }
        }
    }

    public int ProxyPort
    {
        get => _settings.Proxy.Port;
        set
        {
            if (_settings.Proxy.Port != value)
            {
                _settings.Proxy.Port = value;
                OnPropertyChanged();
            }
        }
    }

    public string ProxyUserName
    {
        get => _settings.Proxy.UserName;
        set
        {
            if (_settings.Proxy.UserName != value)
            {
                _settings.Proxy.UserName = value;
                OnPropertyChanged();
            }
        }
    }

    public string ProxyPassword
    {
        get => _settings.Proxy.Password;
        set
        {
            if (_settings.Proxy.Password != value)
            {
                _settings.Proxy.Password = value;
                OnPropertyChanged();
            }
        }
    }
}
