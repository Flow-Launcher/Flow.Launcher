using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Avalonia.Views.Dialogs;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages
{
    public partial class PluginStoreSettingsViewModel : ObservableObject
    {
        public PluginStoreSettingsViewModel()
        {
            SortModes = Enum.GetValues<PluginStoreSortMode>()
                .Select(m => new SortModeItem(m))
                .ToList();
            _selectedSortModeItem = SortModes[0];

            // Fire and forget - load async without blocking
            _ = LoadPluginsAsync();
        }

        public enum PluginStoreSortMode
        {
            Default,
            Name,
            ReleaseDate,
            UpdatedDate
        }

        public sealed class SortModeItem
        {
            public SortModeItem(PluginStoreSortMode value)
            {
                Value = value;
            }

            public PluginStoreSortMode Value { get; }
            public string Display => App.API.GetTranslation($"PluginStoreSortMode{Value}");
        }

        public sealed class PluginStoreGroup
        {
            public PluginStoreGroup(string category, IReadOnlyList<PluginStoreItemViewModel> items)
            {
                Header = App.API.GetTranslation($"pluginStore_{category}");
                Items = items;
            }

            public string Header { get; }
            public IReadOnlyList<PluginStoreItemViewModel> Items { get; }
        }

        public IReadOnlyList<SortModeItem> SortModes { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredPlugins))]
        [NotifyPropertyChangedFor(nameof(PluginGroups))]
        private SortModeItem _selectedSortModeItem;
        
        [ObservableProperty]
        private bool _isLoading;
        
        private async Task LoadPluginsAsync()
        {
            IsLoading = true;
            try
            {
                // First, try to show cached plugins immediately
                LoadPluginsFromManifest();

                // If no cached plugins, fetch from remote
                if (ExternalPlugins.Count == 0)
                {
                    await App.API.UpdatePluginManifestAsync();
                    LoadPluginsFromManifest();
                }
            }
            catch (Exception ex)
            {
                Flow.Launcher.Infrastructure.Logger.Log.Exception(nameof(PluginStoreSettingsViewModel), "Failed to load plugins", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private void LoadPluginsFromManifest()
        {
            var plugins = App.API.GetPluginManifest();
            if (plugins != null && plugins.Count > 0)
            {
                foreach (var old in ExternalPlugins)
                {
                    old.StateChanged -= OnItemStateChanged;
                }

                var items = plugins.Select(p => new PluginStoreItemViewModel(p)).ToList();
                foreach (var item in items)
                {
                    item.StateChanged += OnItemStateChanged;
                }
                ExternalPlugins = items;
            }
        }

        private void OnItemStateChanged()
        {
            OnPropertyChanged(nameof(FilteredPlugins));
            OnPropertyChanged(nameof(PluginGroups));
        }

        private void RefreshAllItemStates()
        {
            foreach (var item in ExternalPlugins)
            {
                item.RefreshState();
            }
            OnItemStateChanged();
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredPlugins))]
        [NotifyPropertyChangedFor(nameof(PluginGroups))]
        private string _filterText = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredPlugins))]
        [NotifyPropertyChangedFor(nameof(PluginGroups))]
        private bool _showDotNet = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredPlugins))]
        [NotifyPropertyChangedFor(nameof(PluginGroups))]
        private bool _showPython = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredPlugins))]
        [NotifyPropertyChangedFor(nameof(PluginGroups))]
        private bool _showNodeJs = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredPlugins))]
        [NotifyPropertyChangedFor(nameof(PluginGroups))]
        private bool _showExecutable = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredPlugins))]
        [NotifyPropertyChangedFor(nameof(PluginGroups))]
        private IList<PluginStoreItemViewModel> _externalPlugins = new List<PluginStoreItemViewModel>();

        public IEnumerable<PluginStoreItemViewModel> FilteredPlugins
        {
            get
            {
                if (ExternalPlugins == null) return new List<PluginStoreItemViewModel>();

                return GetSortedPlugins(ExternalPlugins.Where(SatisfiesFilter));
            }
        }

        /// <summary>
        /// Filtered + sorted plugins grouped by category (New release / Recently updated / Plugins / Installed in
        /// default mode; Plugins / Installed otherwise), preserving sort order.
        /// </summary>
        public IReadOnlyList<PluginStoreGroup> PluginGroups
        {
            get
            {
                var isDefault = CurrentSortMode == PluginStoreSortMode.Default;
                return FilteredPlugins
                    .GroupBy(p => isDefault ? p.DefaultCategory : p.InstallCategory)
                    .Select(g => new PluginStoreGroup(g.Key, g.ToList()))
                    .ToList();
            }
        }

        // The ComboBox may transiently push null while its items change.
        private PluginStoreSortMode CurrentSortMode => SelectedSortModeItem?.Value ?? PluginStoreSortMode.Default;

        private IList<PluginStoreItemViewModel> GetSortedPlugins(IEnumerable<PluginStoreItemViewModel> plugins)
        {
            return CurrentSortMode switch
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

        private bool SatisfiesFilter(PluginStoreItemViewModel plugin)
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
            if (string.IsNullOrEmpty(FilterText)) return true;

            var nameMatch = App.API.FuzzySearch(FilterText, plugin.Name);
            var descMatch = App.API.FuzzySearch(FilterText, plugin.Description);

            return nameMatch.IsSearchPrecisionScoreMet() || descMatch.IsSearchPrecisionScoreMet();
        }

        [RelayCommand]
        private async Task RefreshExternalPluginsAsync()
        {
            IsLoading = true;
            try
            {
                // Fetch fresh data from remote
                await App.API.UpdatePluginManifestAsync();
                // Reload from manifest (whether update succeeded or not, use latest cached)
                LoadPluginsFromManifest();
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task InstallPluginAsync()
        {
            // In Avalonia we need a window to show the dialog.
            // We can get the top level window or pass it as a parameter.
            // For now, let's assume we can get the active window or use a service.
            // Since we are in a ViewModel, we should avoid direct UI references if possible,
            // but for file dialogs it's common to need a TopLevel.
            
            var topLevel = TopLevel.GetTopLevel(global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);
            
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = App.API.GetTranslation("SelectZipFile"),
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Zip Files") { Patterns = new[] { "*.zip" } } }
            });

            if (files.Count > 0)
            {
                var file = files[0].Path.LocalPath;
                if (!string.IsNullOrEmpty(file))
                {
                    await PluginInstaller.InstallPluginAndCheckRestartAsync(file);
                    RefreshAllItemStates();
                }
            }
        }

        [RelayCommand]
        private async Task CheckPluginUpdatesAsync()
        {
            await PluginInstaller.CheckForPluginUpdatesAsync(
                plugins => Dispatcher.UIThread.InvokeAsync(() => ShowPluginUpdateWindowAsync(plugins)),
                silentUpdate: false);
        }

        private async Task ShowPluginUpdateWindowAsync(List<PluginUpdateInfo> plugins)
        {
            try
            {
                if (global::Avalonia.Application.Current?.ApplicationLifetime is not global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop ||
                    desktop.MainWindow is null)
                {
                    return;
                }

                var dialog = new PluginUpdateWindow(plugins);
                if (await dialog.ShowDialog<bool>(desktop.MainWindow))
                {
                    RefreshAllItemStates();
                }
            }
            catch (Exception ex)
            {
                App.API?.LogException(nameof(PluginStoreSettingsViewModel), "Failed to show plugin update window", ex);
            }
        }
        
        [RelayCommand]
        private void ClearFilterText()
        {
            FilterText = string.Empty;
        }
    }
}
