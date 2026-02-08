using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Avalonia.Resource;
using Avalonia;
using Avalonia.Styling;
using System.Collections.Generic;
using System.Linq;
using System;
using AvaloniaI18n = Flow.Launcher.Avalonia.Resource.Internationalization;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class ThemeSettingsViewModel : ObservableObject
{
    private readonly Settings _settings;
    private readonly AvaloniaI18n _i18n;

    public ThemeSettingsViewModel()
    {
        _settings = Ioc.Default.GetRequiredService<Settings>();
        _i18n = Ioc.Default.GetRequiredService<AvaloniaI18n>();
        ColorSchemeOptions = DropdownDataGeneric<ColorSchemes>.GetEnumData("ColorScheme");

        _settings.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Settings.Language))
            {
                UpdateLabels();
            }
        };
    }

    public List<DropdownDataGeneric<ColorSchemes>> ColorSchemeOptions { get; }

    public ColorSchemes SelectedColorScheme
    {
        get => Enum.TryParse<ColorSchemes>(_settings.Theme, out var result) ? result : ColorSchemes.System;
        set
        {
            if (SelectedColorScheme != value)
            {
                var themeString = value.ToString();
                _settings.Theme = themeString;
                ApplyTheme(themeString);
                OnPropertyChanged();
            }
        }
    }

    private void UpdateLabels()
    {
        ColorSchemeOptions.ForEach(x => x.UpdateLabels());
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
