using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.UserSettings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AvaloniaI18n = Flow.Launcher.Avalonia.Resource.Internationalization;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class GeneralSettingsViewModel : ObservableObject
{
    private readonly Flow.Launcher.Infrastructure.UserSettings.Settings _settings;
    private readonly AvaloniaI18n _i18n;

    public GeneralSettingsViewModel()
    {
        _settings = Ioc.Default.GetRequiredService<Flow.Launcher.Infrastructure.UserSettings.Settings>();
        _i18n = Ioc.Default.GetRequiredService<AvaloniaI18n>();
        
        LoadLanguages();
    }

    [ObservableProperty]
    private List<Language> _languages = new();

    public Language? SelectedLanguage
    {
        get => Languages.FirstOrDefault(l => l.LanguageCode == _settings.Language);
        set
        {
            if (value != null && value.LanguageCode != _settings.Language)
            {
                _settings.Language = value.LanguageCode;
                _i18n.ChangeLanguage(value.LanguageCode);
                OnPropertyChanged();
            }
        }
    }

    public bool StartOnStartup
    {
        get => _settings.StartFlowLauncherOnSystemStartup;
        set
        {
            _settings.StartFlowLauncherOnSystemStartup = value;
            OnPropertyChanged();
        }
    }

    public bool HideWhenDeactivated
    {
        get => _settings.HideWhenDeactivated;
        set
        {
            _settings.HideWhenDeactivated = value;
            OnPropertyChanged();
        }
    }

    public bool ShowAtTopmost
    {
        get => _settings.ShowAtTopmost;
        set
        {
            _settings.ShowAtTopmost = value;
            OnPropertyChanged();
        }
    }

    public string PythonPath => _settings.PluginSettings.PythonExecutablePath ?? "Not set";
    public string NodePath => _settings.PluginSettings.NodeExecutablePath ?? "Not set";

    [RelayCommand]
    private async Task SelectPython()
    {
        // TODO: Implement file picker
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SelectNode()
    {
        // TODO: Implement file picker
        await Task.CompletedTask;
    }

    private void LoadLanguages()
    {
        // Minimal set of languages for now, can be expanded by loading from directory later
        Languages = new List<Language>
        {
            new Language("en", "English"),
            new Language("zh-cn", "中文 (简体)"),
            new Language("zh-tw", "中文 (繁體)"),
            new Language("ko", "한국어"),
            new Language("ja", "日本語")
        };
    }
}
