using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Version = SemanticVersioning.Version;

namespace Flow.Launcher.ViewModel
{
    public partial class PluginStoreItemViewModel : BaseModel
    {
        private static readonly string ClassName = nameof(PluginStoreItemViewModel);
        
        private static readonly Settings Settings = Ioc.Default.GetRequiredService<Settings>();

        private readonly UserPlugin _newPlugin;
        private readonly PluginPair _oldPluginPair;

        public PluginStoreItemViewModel(UserPlugin plugin)
        {
            _newPlugin = plugin;
            _oldPluginPair = PluginManager.GetPluginForId(plugin.ID);
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

        public bool LabelInstalled => _oldPluginPair != null;
        public bool LabelUpdate => LabelInstalled && new Version(_newPlugin.Version) > new Version(_oldPluginPair.Metadata.Version);

        internal const string None = "None";
        internal const string RecentlyUpdated = "RecentlyUpdated";
        internal const string NewRelease = "NewRelease";
        internal const string Installed = "Installed";

        public string Category
        {
            get
            {
                string category = None;
                if (DateTime.Now - _newPlugin.LatestReleaseDate < TimeSpan.FromDays(7))
                {
                    category = RecentlyUpdated;
                }
                if (DateTime.Now - _newPlugin.DateAdded < TimeSpan.FromDays(7))
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

        [RelayCommand]
        private async Task ShowCommandQueryAsync(string action)
        {
            switch (action)
            {
                case "install":
                    await InstallPluginAsync(_newPlugin);
                    break;
                case "uninstall":
                    await UninstallPluginAsync(_oldPluginPair.Metadata);
                    break;
                case "update":
                    await UpdatePluginAsync(_newPlugin, _oldPluginPair.Metadata);
                    break;
                default:
                    break;
            }
        }

        internal static async Task InstallPluginAsync(UserPlugin newPlugin)
        {
            if (App.API.ShowMsgBox(
                string.Format(
                    App.API.GetTranslation("InstallPromptSubtitle"),
                    newPlugin.Name, newPlugin.Author, Environment.NewLine),
                App.API.GetTranslation("InstallPromptTitle"),
                button: MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            try
            {
                // at minimum should provide a name, but handle plugin that is not downloaded from plugins manifest and is a url download
                var downloadFilename = string.IsNullOrEmpty(newPlugin.Version)
                    ? $"{newPlugin.Name}-{Guid.NewGuid()}.zip"
                    : $"{newPlugin.Name}-{newPlugin.Version}.zip";

                var filePath = Path.Combine(Path.GetTempPath(), downloadFilename);

                using var cts = new CancellationTokenSource();

                if (!newPlugin.IsFromLocalInstallPath)
                {
                    await DownloadFileAsync(
                        $"{App.API.GetTranslation("DownloadingPlugin")} {newPlugin.Name}",
                        newPlugin.UrlDownload, filePath, cts);
                }
                else
                {
                    filePath = newPlugin.LocalInstallPath;
                }

                // check if user cancelled download before installing plugin
                if (cts.IsCancellationRequested)
                {
                    return;
                }
                else
                {
                    if (!File.Exists(filePath))
                    {
                        throw new FileNotFoundException($"Plugin {newPlugin.ID} zip file not found at {filePath}", filePath);
                    }

                    App.API.InstallPlugin(newPlugin, filePath);

                    if (!newPlugin.IsFromLocalInstallPath)
                    {
                        File.Delete(filePath);
                    }
                }
            }
            catch (Exception e)
            {
                App.API.LogException(ClassName, "Failed to install plugin", e);
                App.API.ShowMsgError(App.API.GetTranslation("ErrorInstallingPlugin"));
                return; // don’t restart on failure
            }

            if (Settings.AutoRestartAfterChanging)
            {
                App.API.RestartApp();
            }
            else
            {
                App.API.ShowMsg(
                    App.API.GetTranslation("installbtn"),
                    string.Format(
                        App.API.GetTranslation(
                            "InstallSuccessNoRestart"),
                        newPlugin.Name));
            }
        }

        internal static async Task UninstallPluginAsync(PluginMetadata oldPlugin)
        {
            if (App.API.ShowMsgBox(
                string.Format(
                    App.API.GetTranslation("UninstallPromptSubtitle"),
                    oldPlugin.Name, oldPlugin.Author, Environment.NewLine),
                App.API.GetTranslation("UninstallPromptTitle"),
                button: MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            var removePluginSettings = App.API.ShowMsgBox(
                App.API.GetTranslation("KeepPluginSettingsSubtitle"),
                App.API.GetTranslation("KeepPluginSettingsTitle"),
                button: MessageBoxButton.YesNo) == MessageBoxResult.No;

            try
            {
                await App.API.UninstallPluginAsync(oldPlugin, removePluginSettings);
            }
            catch (Exception e)
            {
                App.API.LogException(ClassName, "Failed to uninstall plugin", e);
                App.API.ShowMsgError(App.API.GetTranslation("ErrorUninstallingPlugin"));
                return; // don’t restart on failure
            }

            if (Settings.AutoRestartAfterChanging)
            {
                App.API.RestartApp();
            }
            else
            {
                App.API.ShowMsg(
                    App.API.GetTranslation("uninstallbtn"),
                    string.Format(
                        App.API.GetTranslation(
                            "UninstallSuccessNoRestart"),
                        oldPlugin.Name));
            }
        }

        internal static async Task UpdatePluginAsync(UserPlugin newPlugin, PluginMetadata oldPlugin)
        {
            if (App.API.ShowMsgBox(
                string.Format(
                    App.API.GetTranslation("UpdatePromptSubtitle"),
                    oldPlugin.Name, oldPlugin.Author, Environment.NewLine),
                App.API.GetTranslation("UpdatePromptTitle"),
                button: MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            try
            {
                var filePath = Path.Combine(Path.GetTempPath(), $"{newPlugin.Name}-{newPlugin.Version}.zip");

                using var cts = new CancellationTokenSource();

                if (!newPlugin.IsFromLocalInstallPath)
                {
                    await DownloadFileAsync(
                        $"{App.API.GetTranslation("DownloadingPlugin")} {newPlugin.Name}",
                        newPlugin.UrlDownload, filePath, cts);
                }
                else
                {
                    filePath = newPlugin.LocalInstallPath;
                }

                // check if user cancelled download before installing plugin
                if (cts.IsCancellationRequested)
                {
                    return;
                }
                else
                {
                    await App.API.UpdatePluginAsync(oldPlugin, newPlugin, filePath);
                }
            }
            catch (Exception e)
            {
                App.API.LogException(ClassName, "Failed to update plugin", e);
                App.API.ShowMsgError(App.API.GetTranslation("ErrorUpdatingPlugin"));
                return; // don’t restart on failure
            }

            if (Settings.AutoRestartAfterChanging)
            {
                App.API.RestartApp();
            }
            else
            {
                App.API.ShowMsg(
                    App.API.GetTranslation("updatebtn"),
                    string.Format(
                        App.API.GetTranslation(
                            "UpdateSuccessNoRestart"),
                        newPlugin.Name));
            }
        }

        private static async Task DownloadFileAsync(string prgBoxTitle, string downloadUrl, string filePath, CancellationTokenSource cts, bool deleteFile = true, bool showProgress = true)
        {
            if (deleteFile && File.Exists(filePath))
                File.Delete(filePath);

            if (showProgress)
            {
                var exceptionHappened = false;
                await App.API.ShowProgressBoxAsync(prgBoxTitle,
                    async (reportProgress) =>
                    {
                        if (reportProgress == null)
                        {
                            // when reportProgress is null, it means there is expcetion with the progress box
                            // so we record it with exceptionHappened and return so that progress box will close instantly
                            exceptionHappened = true;
                            return;
                        }
                        else
                        {
                            await App.API.HttpDownloadAsync(downloadUrl, filePath, reportProgress, cts.Token).ConfigureAwait(false);
                        }
                    }, cts.Cancel);

                // if exception happened while downloading and user does not cancel downloading,
                // we need to redownload the plugin
                if (exceptionHappened && (!cts.IsCancellationRequested))
                    await App.API.HttpDownloadAsync(downloadUrl, filePath, token: cts.Token).ConfigureAwait(false);
            }
            else
            {
                await App.API.HttpDownloadAsync(downloadUrl, filePath, token: cts.Token).ConfigureAwait(false);
            }
        }
    }
}
