using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.PluginsManager.Models;
using Flow.Launcher.Plugin.SharedCommands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Flow.Launcher.Plugin.PluginsManager
{
    internal class PluginsManager
    {
        private PluginsManifest pluginsManifest;

        private PluginInitContext Context { get; set; }

        private Settings Settings { get; set; }

        private bool shouldHideWindow = true;

        private bool ShouldHideWindow
        {
            set { shouldHideWindow = value; }
            get
            {
                var setValue = shouldHideWindow;
                // Default value for hide main window is true. Revert after get call.
                // This ensures when set by another method to false, it is only used once.
                shouldHideWindow = true;

                return setValue;
            }
        }

        internal readonly string icoPath = "Images\\pluginsmanager.png";

        internal PluginsManager(PluginInitContext context, Settings settings)
        {
            pluginsManifest = new PluginsManifest();
            Context = context;
            Settings = settings;
        }

        private Task _downloadManifestTask = Task.CompletedTask;


        internal Task UpdateManifest()
        {
            if (_downloadManifestTask.Status == TaskStatus.Running)
            {
                return _downloadManifestTask;
            }
            else
            {
                _downloadManifestTask = pluginsManifest.DownloadManifest();
                _downloadManifestTask.ContinueWith(_ =>
                        Context.API.ShowMsg(Context.API.GetTranslation("plugin_pluginsmanager_update_failed_title"),
                            Context.API.GetTranslation("plugin_pluginsmanager_update_failed_subtitle"), icoPath, false),
                    TaskContinuationOptions.OnlyOnFaulted);
                return _downloadManifestTask;
            }
        }

        internal List<Result> GetDefaultHotKeys()
        {
            return new List<Result>()
            {
                new Result()
                {
                    Title = Settings.HotKeyInstall,
                    IcoPath = icoPath,
                    Action = _ =>
                    {
                        Context.API.ChangeQuery("pm install ");
                        return false;
                    }
                },
                new Result()
                {
                    Title = Settings.HotkeyUninstall,
                    IcoPath = icoPath,
                    Action = _ =>
                    {
                        Context.API.ChangeQuery("pm uninstall ");
                        return false;
                    }
                },
                new Result()
                {
                    Title = Settings.HotkeyUpdate,
                    IcoPath = icoPath,
                    Action = _ =>
                    {
                        Context.API.ChangeQuery("pm update ");
                        return false;
                    }
                }
            };
        }

        internal async Task InstallOrUpdate(UserPlugin plugin)
        {
            if (PluginExists(plugin.ID))
            {
                if (Context.API.GetAllPlugins()
                    .Any(x => x.Metadata.ID == plugin.ID && x.Metadata.Version.CompareTo(plugin.Version) < 0))
                {
                    if (MessageBox.Show(Context.API.GetTranslation("plugin_pluginsmanager_update_exists"),
                        Context.API.GetTranslation("plugin_pluginsmanager_update_title"),
                        MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        Context
                            .API
                            .ChangeQuery(
                                $"{Context.CurrentPluginMetadata.ActionKeywords.FirstOrDefault()} {Settings.HotkeyUpdate} {plugin.Name}");

                    var mainWindow = Application.Current.MainWindow;
                    mainWindow.Visibility = Visibility.Visible;
                    mainWindow.Focus();

                    shouldHideWindow = false;

                    return;
                }

                Context.API.ShowMsg(Context.API.GetTranslation("plugin_pluginsmanager_update_alreadyexists"));
                return;
            }

            var message = string.Format(Context.API.GetTranslation("plugin_pluginsmanager_install_prompt"),
                plugin.Name, plugin.Author,
                Environment.NewLine, Environment.NewLine);

            if (MessageBox.Show(message, Context.API.GetTranslation("plugin_pluginsmanager_install_title"),
                MessageBoxButton.YesNo) == MessageBoxResult.No)
                return;

            var filePath = Path.Combine(DataLocation.PluginsDirectory, $"{plugin.Name}-{plugin.Version}.zip");

            try
            {
                Context.API.ShowMsg(Context.API.GetTranslation("plugin_pluginsmanager_downloading_plugin"),
                    Context.API.GetTranslation("plugin_pluginsmanager_please_wait"));

                await Http.DownloadAsync(plugin.UrlDownload, filePath).ConfigureAwait(false);

                Context.API.ShowMsg(Context.API.GetTranslation("plugin_pluginsmanager_downloading_plugin"),
                    Context.API.GetTranslation("plugin_pluginsmanager_download_success"));

                Install(plugin, filePath);
            }
            catch (Exception e)
            {
                Context.API.ShowMsg(Context.API.GetTranslation("plugin_pluginsmanager_install_error_title"),
                    string.Format(Context.API.GetTranslation("plugin_pluginsmanager_install_error_subtitle"),
                        plugin.Name));

                Log.Exception("PluginsManager", "An error occured while downloading plugin", e, "InstallOrUpdate");

                return;
            }

            Context.API.RestartApp();
        }

        internal async ValueTask<List<Result>> RequestUpdate(string search, CancellationToken token)
        {
            if (!pluginsManifest.UserPlugins.Any())
            {
                await UpdateManifest();
            }

            token.ThrowIfCancellationRequested();

            var autocompletedResults = AutoCompleteReturnAllResults(search,
                Settings.HotkeyUpdate,
                "Update",
                "Select a plugin to update");

            if (autocompletedResults.Any())
                return autocompletedResults;

            var uninstallSearch = search.Replace(Settings.HotkeyUpdate, string.Empty).TrimStart();

            var resultsForUpdate =
                from existingPlugin in Context.API.GetAllPlugins()
                join pluginFromManifest in pluginsManifest.UserPlugins
                    on existingPlugin.Metadata.ID equals pluginFromManifest.ID
                where existingPlugin.Metadata.Version.CompareTo(pluginFromManifest.Version) <
                      0 // if current version precedes manifest version
                select
                    new
                    {
                        pluginFromManifest.Name,
                        pluginFromManifest.Author,
                        CurrentVersion = existingPlugin.Metadata.Version,
                        NewVersion = pluginFromManifest.Version,
                        existingPlugin.Metadata.IcoPath,
                        PluginExistingMetadata = existingPlugin.Metadata,
                        PluginNewUserPlugin = pluginFromManifest
                    };

            if (!resultsForUpdate.Any())
                return new List<Result>
                {
                    new Result
                    {
                        Title = Context.API.GetTranslation("plugin_pluginsmanager_update_noresult_title"),
                        SubTitle = Context.API.GetTranslation("plugin_pluginsmanager_update_noresult_subtitle"),
                        IcoPath = icoPath
                    }
                };

            var results = resultsForUpdate
                .Select(x =>
                    new Result
                    {
                        Title = $"{x.Name} by {x.Author}",
                        SubTitle = $"Update from version {x.CurrentVersion} to {x.NewVersion}",
                        IcoPath = x.IcoPath,
                        Action = e =>
                        {
                            string message = string.Format(
                                Context.API.GetTranslation("plugin_pluginsmanager_update_prompt"),
                                x.Name, x.Author,
                                Environment.NewLine, Environment.NewLine);

                            if (MessageBox.Show(message,
                                Context.API.GetTranslation("plugin_pluginsmanager_update_title"),
                                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                            {
                                Uninstall(x.PluginExistingMetadata, false);

                                var downloadToFilePath = Path.Combine(DataLocation.PluginsDirectory,
                                    $"{x.Name}-{x.NewVersion}.zip");

                                Task.Run(async delegate
                                {
                                    Context.API.ShowMsg(
                                        Context.API.GetTranslation("plugin_pluginsmanager_downloading_plugin"),
                                        Context.API.GetTranslation("plugin_pluginsmanager_please_wait"));

                                    await Http.DownloadAsync(x.PluginNewUserPlugin.UrlDownload, downloadToFilePath)
                                        .ConfigureAwait(false);

                                    Context.API.ShowMsg(
                                        Context.API.GetTranslation("plugin_pluginsmanager_downloading_plugin"),
                                        Context.API.GetTranslation("plugin_pluginsmanager_download_success"));

                                    Install(x.PluginNewUserPlugin, downloadToFilePath);

                                    Context.API.RestartApp();
                                }).ContinueWith(t =>
                                {
                                    Log.Exception("PluginsManager", $"Update failed for {x.Name}",
                                        t.Exception.InnerException, "RequestUpdate");
                                    Context.API.ShowMsg(
                                        Context.API.GetTranslation("plugin_pluginsmanager_install_error_title"),
                                        string.Format(
                                            Context.API.GetTranslation("plugin_pluginsmanager_install_error_subtitle"),
                                            x.Name));
                                }, TaskContinuationOptions.OnlyOnFaulted);

                                return true;
                            }

                            return false;
                        },
                        ContextData = 
                            new UserPlugin
                            {
                                Website = x.PluginNewUserPlugin.Website,
                                UrlSourceCode = x.PluginNewUserPlugin.UrlSourceCode
                            }
                    });

            return Search(results, uninstallSearch);
        }

        internal bool PluginExists(string id)
        {
            return Context.API.GetAllPlugins().Any(x => x.Metadata.ID == id);
        }

        internal List<Result> Search(IEnumerable<Result> results, string searchName)
        {
            if (string.IsNullOrEmpty(searchName))
                return results.ToList();

            return results
                .Where(x =>
                {
                    var matchResult = StringMatcher.FuzzySearch(searchName, x.Title);
                    if (matchResult.IsSearchPrecisionScoreMet())
                        x.Score = matchResult.Score;

                    return matchResult.IsSearchPrecisionScoreMet();
                })
                .ToList();
        }

        internal async ValueTask<List<Result>> RequestInstallOrUpdate(string searchName, CancellationToken token)
        {
            if (!pluginsManifest.UserPlugins.Any())
            {
                await UpdateManifest();
            }

            token.ThrowIfCancellationRequested();

            var searchNameWithoutKeyword = searchName.Replace(Settings.HotKeyInstall, string.Empty).Trim();

            var results =
                pluginsManifest
                    .UserPlugins
                    .Select(x =>
                        new Result
                        {
                            Title = $"{x.Name} by {x.Author}",
                            SubTitle = x.Description,
                            IcoPath = icoPath,
                            Action = e =>
                            {
                                if (e.SpecialKeyState.CtrlPressed)
                                {
                                    SearchWeb.NewTabInBrowser(x.Website);
                                    return ShouldHideWindow;
                                }

                                Application.Current.MainWindow.Hide();
                                _ = InstallOrUpdate(x); // No need to wait
                                return ShouldHideWindow;
                            },
                            ContextData = x
                        });

            return Search(results, searchNameWithoutKeyword);
        }

        private void Install(UserPlugin plugin, string downloadedFilePath)
        {
            if (!File.Exists(downloadedFilePath))
                return;

            var tempFolderPath = Path.Combine(Path.GetTempPath(), "flowlauncher");
            var tempFolderPluginPath = Path.Combine(tempFolderPath, "plugin");

            if (Directory.Exists(tempFolderPath))
                Directory.Delete(tempFolderPath, true);

            Directory.CreateDirectory(tempFolderPath);

            var zipFilePath = Path.Combine(tempFolderPath, Path.GetFileName(downloadedFilePath));

            File.Copy(downloadedFilePath, zipFilePath);

            File.Delete(downloadedFilePath);

            Utilities.UnZip(zipFilePath, tempFolderPluginPath, true);

            var pluginFolderPath = Utilities.GetContainingFolderPathAfterUnzip(tempFolderPluginPath);

            var metadataJsonFilePath = string.Empty;
            if (File.Exists(Path.Combine(pluginFolderPath, Constant.PluginMetadataFileName)))
                metadataJsonFilePath = Path.Combine(pluginFolderPath, Constant.PluginMetadataFileName);

            if (string.IsNullOrEmpty(metadataJsonFilePath) || string.IsNullOrEmpty(pluginFolderPath))
            {
                MessageBox.Show(Context.API.GetTranslation("plugin_pluginsmanager_install_errormetadatafile"));
                return;
            }

            string newPluginPath = Path.Combine(DataLocation.PluginsDirectory, $"{plugin.Name}-{plugin.Version}");

            FilesFolders.CopyAll(pluginFolderPath, newPluginPath);

            Directory.Delete(pluginFolderPath, true);
        }

        internal List<Result> RequestUninstall(string search)
        {
            var autocompletedResults = AutoCompleteReturnAllResults(search,
                Settings.HotkeyUninstall,
                "Uninstall",
                "Select a plugin to uninstall");

            if (autocompletedResults.Any())
                return autocompletedResults;

            var uninstallSearch = search.Replace(Settings.HotkeyUninstall, string.Empty).TrimStart();

            var results = Context.API
                .GetAllPlugins()
                .Select(x =>
                    new Result
                    {
                        Title = $"{x.Metadata.Name} by {x.Metadata.Author}",
                        SubTitle = x.Metadata.Description,
                        IcoPath = x.Metadata.IcoPath,
                        Action = e =>
                        {
                            string message = string.Format(
                                Context.API.GetTranslation("plugin_pluginsmanager_uninstall_prompt"),
                                x.Metadata.Name, x.Metadata.Author,
                                Environment.NewLine, Environment.NewLine);

                            if (MessageBox.Show(message,
                                Context.API.GetTranslation("plugin_pluginsmanager_uninstall_title"),
                                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                            {
                                Application.Current.MainWindow.Hide();
                                Uninstall(x.Metadata);
                                Context.API.RestartApp();

                                return true;
                            }

                            return false;
                        }
                    });

            return Search(results, uninstallSearch);
        }

        private void Uninstall(PluginMetadata plugin, bool removedSetting = true)
        {
            if (removedSetting)
            {
                PluginManager.Settings.Plugins.Remove(plugin.ID);
                PluginManager.AllPlugins.RemoveAll(p => p.Metadata.ID == plugin.ID);
            }

            // Marked for deletion. Will be deleted on next start up
            using var _ = File.CreateText(Path.Combine(plugin.PluginDirectory, "NeedDelete.txt"));
        }

        private List<Result> AutoCompleteReturnAllResults(string search, string hotkey, string title, string subtitle)
        {
            if (!string.IsNullOrEmpty(search)
                && hotkey.StartsWith(search)
                && (hotkey != search || !search.StartsWith(hotkey)))
            {
                return
                    new List<Result>
                    {
                        new Result
                        {
                            Title = title,
                            IcoPath = icoPath,
                            SubTitle = subtitle,
                            Action = e =>
                            {
                                Context
                                    .API
                                    .ChangeQuery(
                                        $"{Context.CurrentPluginMetadata.ActionKeywords.FirstOrDefault()} {hotkey} ");

                                return false;
                            }
                        }
                    };
            }

            return new List<Result>();
        }
    }
}