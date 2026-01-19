using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Plugin.Program.Views.Models;
using Flow.Launcher.Plugin.Program.Views.Commands;
using Flow.Launcher.Plugin.Program.Programs;
using System.IO;
using System.Windows;
using Flow.Launcher.Plugin.Program.Views;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace Flow.Launcher.Plugin.Program.ViewModels;

public partial class ProgramSettingViewModel : ObservableObject
{
    public static IValueConverter EnabledToColorConverter { get; } = new FuncValueConverter<bool, IBrush>(enabled => 
        enabled ? Brushes.Green : Brushes.Red);

    public static IValueConverter IsNotNullConverter { get; } = new FuncValueConverter<object?, bool>(obj => obj != null);

    private readonly PluginInitContext _context;
    private readonly Settings _settings;

    [ObservableProperty]
    private bool _enableUWP;

    [ObservableProperty]
    private bool _enableStartMenuSource;

    [ObservableProperty]
    private bool _enableRegistrySource;

    [ObservableProperty]
    private bool _enablePATHSource;

    [ObservableProperty]
    private bool _hideAppsPath;

    [ObservableProperty]
    private bool _hideUninstallers;

    [ObservableProperty]
    private bool _enableDescription;

    [ObservableProperty]
    private bool _hideDuplicatedWindowsApp;

    public bool ShowUWPCheckbox => UWPPackage.SupportUWP();

    [ObservableProperty]
    private ObservableCollection<ProgramSource> _programSources = new();

    [ObservableProperty]
    private ProgramSource? _selectedProgramSource;

    [ObservableProperty]
    private bool _isIndexing;

    [ObservableProperty]
    private string _customSuffixes;

    [ObservableProperty]
    private string _customProtocols;

    public ProgramSettingViewModel(PluginInitContext context, Settings settings)
    {
        _context = context;
        _settings = settings;

        _enableUWP = _settings.EnableUWP;
        _enableStartMenuSource = _settings.EnableStartMenuSource;
        _enableRegistrySource = _settings.EnableRegistrySource;
        _enablePATHSource = _settings.EnablePathSource;
        _hideAppsPath = _settings.HideAppsPath;
        _hideUninstallers = _settings.HideUninstallers;
        _enableDescription = _settings.EnableDescription;
        _hideDuplicatedWindowsApp = _settings.HideDuplicatedWindowsApp;
        
        _customSuffixes = string.Join(Settings.SuffixSeparator, _settings.CustomSuffixes);
        _customProtocols = string.Join(Settings.SuffixSeparator, _settings.CustomProtocols);

        LoadProgramSources();
    }

    partial void OnCustomSuffixesChanged(string value)
    {
        _settings.CustomSuffixes = value.Split(Settings.SuffixSeparator, StringSplitOptions.RemoveEmptyEntries);
        Main.ResetCache();
    }

    partial void OnCustomProtocolsChanged(string value)
    {
        _settings.CustomProtocols = value.Split(Settings.SuffixSeparator, StringSplitOptions.RemoveEmptyEntries);
        Main.ResetCache();
    }
    
    public bool AppRefMS
    {
        get => _settings.BuiltinSuffixesStatus["appref-ms"];
        set
        {
            _settings.BuiltinSuffixesStatus["appref-ms"] = value;
            OnPropertyChanged();
            Main.ResetCache();
        }
    }
    
    public bool Exe
    {
        get => _settings.BuiltinSuffixesStatus["exe"];
        set
        {
            _settings.BuiltinSuffixesStatus["exe"] = value;
            OnPropertyChanged();
            Main.ResetCache();
        }
    }
    
    public bool Lnk
    {
        get => _settings.BuiltinSuffixesStatus["lnk"];
        set
        {
            _settings.BuiltinSuffixesStatus["lnk"] = value;
            OnPropertyChanged();
            Main.ResetCache();
        }
    }

    public bool Steam
    {
        get => _settings.BuiltinProtocolsStatus["steam"];
        set
        {
            _settings.BuiltinProtocolsStatus["steam"] = value;
            OnPropertyChanged();
            Main.ResetCache();
        }
    }

    public bool Epic
    {
        get => _settings.BuiltinProtocolsStatus["epic"];
        set
        {
            _settings.BuiltinProtocolsStatus["epic"] = value;
            OnPropertyChanged();
            Main.ResetCache();
        }
    }

    public bool Http
    {
        get => _settings.BuiltinProtocolsStatus["http"];
        set
        {
            _settings.BuiltinProtocolsStatus["http"] = value;
            OnPropertyChanged();
            Main.ResetCache();
        }
    }
    
    public bool UseCustomSuffixes
    {
        get => _settings.UseCustomSuffixes;
        set
        {
            _settings.UseCustomSuffixes = value;
            OnPropertyChanged();
            Main.ResetCache();
        }
    }
    
    public bool UseCustomProtocols
    {
        get => _settings.UseCustomProtocols;
        set
        {
            _settings.UseCustomProtocols = value;
            OnPropertyChanged();
            Main.ResetCache();
        }
    }


    private void LoadProgramSources()
    {
        // For Avalonia, we want to maintain the same display list if possible,
        // but for now let's just use a fresh one from settings
        var sources = ProgramSettingDisplay.LoadProgramSources();
        ProgramSources = new ObservableCollection<ProgramSource>(sources);
        
        // We need to set the static list for ProgramSettingDisplay to work
        ProgramSetting.ProgramSettingDisplayList = sources;
    }

    [RelayCommand]
    private async Task Reindex()
    {
        IsIndexing = true;
        await Main.IndexProgramsAsync();
        IsIndexing = false;
    }

    [RelayCommand]
    private async Task LoadAllPrograms()
    {
        await ProgramSettingDisplay.DisplayAllProgramsAsync();
        // Refresh the observable collection
        ProgramSources = new ObservableCollection<ProgramSource>(ProgramSetting.ProgramSettingDisplayList);
    }

    [RelayCommand]
    private async Task ToggleStatus()
    {
        if (SelectedProgramSource == null) return;
        
        var status = !SelectedProgramSource.Enabled;
        await ProgramSettingDisplay.SetProgramSourcesStatusAsync(new List<ProgramSource> { SelectedProgramSource }, status);
        
        if (status)
            ProgramSettingDisplay.RemoveDisabledFromSettings();
        else
            ProgramSettingDisplay.StoreDisabledInSettings();

        if (await new List<ProgramSource> { SelectedProgramSource }.IsReindexRequiredAsync())
            await Reindex();
            
        // Trigger UI update for the item
        OnPropertyChanged(nameof(ProgramSources));
    }

    [RelayCommand]
    private async Task DeleteSource()
    {
        if (SelectedProgramSource == null) return;
        
        // Check if it's user added
        if (!_settings.ProgramSources.Any(x => x.UniqueIdentifier == SelectedProgramSource.UniqueIdentifier))
        {
            _context.API.ShowMsgBox(_context.API.GetTranslation("flowlauncher_plugin_program_delete_program_source_select_user_added"));
            return;
        }

        if (_context.API.ShowMsgBox(_context.API.GetTranslation("flowlauncher_plugin_program_delete_program_source"),
            string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.No)
        {
            return;
        }

        var toDelete = SelectedProgramSource;
        _settings.ProgramSources.RemoveAll(x => x.UniqueIdentifier == toDelete.UniqueIdentifier);
        ProgramSetting.ProgramSettingDisplayList.Remove(toDelete);
        ProgramSources.Remove(toDelete);

        if (await new List<ProgramSource> { toDelete }.IsReindexRequiredAsync())
            await Reindex();
    }

    [RelayCommand]
    private async Task AddSource()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var path = dialog.SelectedPath;
            if (ProgramSources.Any(x => x.UniqueIdentifier.Equals(path, StringComparison.OrdinalIgnoreCase)))
            {
                _context.API.ShowMsgBox(_context.API.GetTranslation("flowlauncher_plugin_program_duplicate_program_source"));
                return;
            }

            var source = new ProgramSource(path, true);
            _settings.ProgramSources.Add(source);
            ProgramSetting.ProgramSettingDisplayList.Add(source);
            ProgramSources.Add(source);
            
            await Reindex();
        }
    }



    partial void OnEnableUWPChanged(bool value)
    {
        _settings.EnableUWP = value;
        _ = Reindex();
    }

    partial void OnEnableStartMenuSourceChanged(bool value)
    {
        _settings.EnableStartMenuSource = value;
        _ = Reindex();
    }

    partial void OnEnableRegistrySourceChanged(bool value)
    {
        _settings.EnableRegistrySource = value;
        _ = Reindex();
    }

    partial void OnEnablePATHSourceChanged(bool value)
    {
        _settings.EnablePathSource = value;
        _ = Reindex();
    }

    partial void OnHideAppsPathChanged(bool value)
    {
        _settings.HideAppsPath = value;
        Main.ResetCache();
    }

    partial void OnHideUninstallersChanged(bool value)
    {
        _settings.HideUninstallers = value;
        Main.ResetCache();
    }

    partial void OnEnableDescriptionChanged(bool value)
    {
        _settings.EnableDescription = value;
        Main.ResetCache();
    }

    partial void OnHideDuplicatedWindowsAppChanged(bool value)
    {
        _settings.HideDuplicatedWindowsApp = value;
        Main.ResetCache();
    }
}
