using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.UserSettings;
using System;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class ProxySettingsViewModel : ObservableObject
{
    private readonly Settings _settings;

    public ProxySettingsViewModel()
    {
        _settings = Ioc.Default.GetRequiredService<Settings>();
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
