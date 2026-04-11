using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPanePluginStoreViewModel : BaseModel
{
    public class SortModeData : DropdownDataGeneric<PluginStoreSortMode> { }

    public List<SortModeData> SortModes { get; } =
        DropdownDataGeneric<PluginStoreSortMode>.GetValues<SortModeData>("PluginStoreSortMode");

    public SettingsPanePluginStoreViewModel()
    {
        UpdateEnumDropdownLocalizations();
    }

    private PluginStoreSortMode _selectedSortMode = PluginStoreSortMode.Default;
    public PluginStoreSortMode SelectedSortMode
    {
        get => _selectedSortMode;
        set
        {
            if (_selectedSortMode != value)
            {
                _selectedSortMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExternalPlugins));
            }
        }
    }

    private string filterText = string.Empty;
    public string FilterText
    {
        get => filterText;
        set
        {
            if (filterText != value)
            {
                filterText = value;
                OnPropertyChanged();
            }
        }
    }

    private bool showDotNet = true;
    public bool ShowDotNet
    {
        get => showDotNet;
        set
        {
            if (showDotNet != value)
            {
                showDotNet = value;
                OnPropertyChanged();
            }
        }
    }

    private bool showPython = true;
    public bool ShowPython
    {
        get => showPython;
        set
        {
            if (showPython != value)
            {
                showPython = value;
                OnPropertyChanged();
            }
        }
    }

    private bool showNodeJs = true;
    public bool ShowNodeJs
    {
        get => showNodeJs;
        set
        {
            if (showNodeJs != value)
            {
                showNodeJs = value;
                OnPropertyChanged();
            }
        }
    }

    private bool showExecutable = true;
    public bool ShowExecutable
    {
        get => showExecutable;
        set
        {
            if (showExecutable != value)
            {
                showExecutable = value;
                OnPropertyChanged();
            }
        }
    }

    public IList<PluginStoreItemViewModel> ExternalPlugins => GetSortedPlugins(
        App.API.GetPluginManifest().Select(p => new PluginStoreItemViewModel(p)));

    [RelayCommand]
    private async Task RefreshExternalPluginsAsync()
    {
        if (await App.API.UpdatePluginManifestAsync())
        {
            OnPropertyChanged(nameof(ExternalPlugins));
        }
    }

    [RelayCommand]
    private async Task InstallPluginAsync()
    {
        var file = GetFileFromDialog(
            Localize.SelectZipFile(),
            $"{Localize.ZipFiles()} (*.zip)|*.zip");

        if (!string.IsNullOrEmpty(file))
            await PluginInstaller.InstallPluginAndCheckRestartAsync(file);
    }

    [RelayCommand]
    private async Task CheckPluginUpdatesAsync()
    {
        await PluginInstaller.CheckForPluginUpdatesAsync((plugins) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var pluginUpdateWindow = new PluginUpdateWindow(plugins);
                pluginUpdateWindow.ShowDialog();
            });
        }, silentUpdate: false);
    }

    private static string GetFileFromDialog(string title, string filter = "")
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads",
            Multiselect = false,
            CheckFileExists = true,
            CheckPathExists = true,
            Title = title,
            Filter = filter
        };
        var result = dlg.ShowDialog();
        if (result == true)
            return dlg.FileName;

        return string.Empty;
    }

    public bool SatisfiesFilter(PluginStoreItemViewModel plugin)
    {
        // Check plugin language
        var pluginShown = false;
        if (AllowedLanguage.IsDotNet(plugin.Language))
        {
            pluginShown = ShowDotNet;
        }
        else if (AllowedLanguage.IsPython(plugin.Language))
        {
            pluginShown = ShowPython;
        }
        else if (AllowedLanguage.IsNodeJs(plugin.Language))
        {
            pluginShown = ShowNodeJs;
        }
        else if (AllowedLanguage.IsExecutable(plugin.Language))
        {
            pluginShown = ShowExecutable;
        }
        if (!pluginShown) return false;

        // Check plugin name & description
        return string.IsNullOrEmpty(FilterText) ||
            App.API.FuzzySearch(FilterText, plugin.Name).IsSearchPrecisionScoreMet() ||
            App.API.FuzzySearch(FilterText, plugin.Description).IsSearchPrecisionScoreMet();
    }

    private void UpdateEnumDropdownLocalizations()
    {
        DropdownDataGeneric<PluginStoreSortMode>.UpdateLabels(SortModes);
    }

    private IList<PluginStoreItemViewModel> GetSortedPlugins(IEnumerable<PluginStoreItemViewModel> plugins)
    {
        return SelectedSortMode switch
        {
            PluginStoreSortMode.Name => plugins
                .OrderBy(p => p.LabelInstalled)
                .ThenBy(p => p.Name)
                .ToList(),

            PluginStoreSortMode.ReleaseDate => plugins
                .OrderBy(p => p.LabelInstalled)
                .ThenByDescending(p => p.DateAdded.HasValue)
                .ThenByDescending(p => p.DateAdded)
                .ToList(),

            PluginStoreSortMode.UpdatedDate => plugins
                .OrderBy(p => p.LabelInstalled)
                .ThenByDescending(p => p.UpdatedDate.HasValue)
                .ThenByDescending(p => p.UpdatedDate)
                .ToList(),

            _ => plugins
                .OrderByDescending(p => p.DefaultCategory == PluginStoreItemViewModel.NewRelease)
                .ThenByDescending(p => p.DefaultCategory == PluginStoreItemViewModel.RecentlyUpdated)
                .ThenByDescending(p => p.DefaultCategory == PluginStoreItemViewModel.None)
                .ThenByDescending(p => p.DefaultCategory == PluginStoreItemViewModel.Installed)
                .ToList(),
        };
    }

}

public enum PluginStoreSortMode
{
    Default,
    Name,
    ReleaseDate,
    UpdatedDate
}
