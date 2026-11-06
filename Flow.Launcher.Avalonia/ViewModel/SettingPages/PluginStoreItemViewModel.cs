using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Plugin;
using Flow.Launcher.Avalonia.Helper;
using Version = SemanticVersioning.Version;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages
{
    public partial class PluginStoreItemViewModel : ObservableObject
    {
        private readonly UserPlugin _newPlugin;
        private PluginPair? _oldPluginPair;

        public PluginStoreItemViewModel(UserPlugin plugin)
        {
            _newPlugin = plugin;
            _oldPluginPair = PluginManager.GetPluginForId(plugin.ID);
            
            _ = LoadIconAsync();
        }

        /// <summary>
        /// Raised after this item's install state may have changed (install/uninstall/update).
        /// </summary>
        public event Action? StateChanged;

        /// <summary>
        /// Re-reads installed state and notifies all state-dependent bindings.
        /// </summary>
        public void RefreshState()
        {
            _oldPluginPair = PluginManager.GetPluginForId(_newPlugin.ID);
            OnPropertyChanged(nameof(LabelInstalled));
            OnPropertyChanged(nameof(LabelUpdate));
            OnPropertyChanged(nameof(DefaultCategory));
            OnPropertyChanged(nameof(InstallCategory));
        }

        public string ID => _newPlugin.ID;
        public string Name => _newPlugin.Name;
        public string Description => _newPlugin.Description;
        public string Author => _newPlugin.Author;
        public string Version => _newPlugin.Version;
        public string Language => _newPlugin.Language;
        public string Website => _newPlugin.Website;
        public string UrlDownload => _newPlugin.UrlDownload;
        public string UrlSourceCode => _newPlugin.UrlSourceCode;
        public string IcoPath => _newPlugin.IcoPath;
        public DateTime? DateAdded => _newPlugin.DateAdded;
        public DateTime? UpdatedDate => _newPlugin.LatestReleaseDate;

        public bool LabelInstalled => _oldPluginPair != null;
        public bool LabelUpdate => _oldPluginPair != null && new Version(_newPlugin.Version) > new Version(_oldPluginPair.Metadata.Version);

        internal const string None = "None";
        internal const string RecentlyUpdated = "RecentlyUpdated";
        internal const string NewRelease = "NewRelease";
        internal const string Installed = "Installed";

        public string InstallCategory => LabelInstalled ? Installed : None;

        public string DefaultCategory
        {
            get
            {
                string category = None;
                if (_newPlugin.LatestReleaseDate is not null &&
                    DateTime.Now - _newPlugin.LatestReleaseDate < TimeSpan.FromDays(7))
                {
                    category = RecentlyUpdated;
                }
                if (_newPlugin.DateAdded is not null &&
                    DateTime.Now - _newPlugin.DateAdded < TimeSpan.FromDays(7))
                {
                    category = NewRelease;
                }
                if (_oldPluginPair != null)
                {
                    category = Installed;
                }

                return category;
            }
        }

        [ObservableProperty]
        private global::Avalonia.Media.IImage? _icon;

        private async Task LoadIconAsync()
        {
            try 
            {
                Icon = await ImageLoader.LoadAsync(_newPlugin.IcoPath);
            }
            catch
            {
                // Ignore errors, Icon will remain null
            }
        }

        [RelayCommand]
        private async Task Install()
        {
            await PluginInstaller.InstallPluginAndCheckRestartAsync(_newPlugin);
            NotifyStateChanged();
        }

        [RelayCommand]
        private async Task Uninstall()
        {
            if (_oldPluginPair != null)
            {
                await PluginInstaller.UninstallPluginAndCheckRestartAsync(_oldPluginPair.Metadata);
                NotifyStateChanged();
            }
        }

        [RelayCommand]
        private async Task Update()
        {
            if (_oldPluginPair != null)
            {
                await PluginInstaller.UpdatePluginAndCheckRestartAsync(_newPlugin, _oldPluginPair.Metadata);
                NotifyStateChanged();
            }
        }

        private void NotifyStateChanged()
        {
            RefreshState();
            StateChanged?.Invoke();
        }
        
        [RelayCommand]
        private void OpenUrl(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                App.API.OpenUrl(url);
            }
        }
    }
}
