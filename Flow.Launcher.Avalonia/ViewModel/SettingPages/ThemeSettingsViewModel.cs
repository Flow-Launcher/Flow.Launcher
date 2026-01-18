using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Avalonia.Resource;
using FluentAvalonia.Styling;
using Avalonia;
using Avalonia.Styling;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class ThemeSettingsViewModel : ObservableObject
{
    private readonly Settings _settings;

    public ThemeSettingsViewModel()
    {
        _settings = Ioc.Default.GetRequiredService<Settings>();
    }

    public List<string> ThemeVariants => new() { "System", "Light", "Dark" };

    public string SelectedThemeVariant
    {
        get => _settings.Theme switch
        {
            "Light" => "Light",
            "Dark" => "Dark",
            _ => "System"
        };
        set
        {
            if (value != SelectedThemeVariant)
            {
                _settings.Theme = value;
                ApplyTheme(value);
                OnPropertyChanged();
            }
        }
    }

    private void ApplyTheme(string variant)
    {
        if (Application.Current == null) return;

        Application.Current.RequestedThemeVariant = variant switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    public int MaxResults
    {
        get => _settings.MaxResultsToShow;
        set
        {
            if (_settings.MaxResultsToShow != value)
            {
                _settings.MaxResultsToShow = value;
                OnPropertyChanged();
            }
        }
    }

    public List<int> MaxResultsRange => Enumerable.Range(1, 20).ToList();

    public bool UseGlyphIcons
    {
        get => _settings.UseGlyphIcons;
        set
        {
            if (_settings.UseGlyphIcons != value)
            {
                _settings.UseGlyphIcons = value;
                OnPropertyChanged();
            }
        }
    }

    public double QueryBoxFontSize
    {
        get => _settings.QueryBoxFontSize;
        set
        {
            _settings.QueryBoxFontSize = value;
            OnPropertyChanged();
        }
    }

    public double ResultItemFontSize
    {
        get => _settings.ResultItemFontSize;
        set
        {
            _settings.ResultItemFontSize = value;
            OnPropertyChanged();
        }
    }
}
