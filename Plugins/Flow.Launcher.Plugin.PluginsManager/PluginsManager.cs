﻿using Flow.Launcher.Core.ExternalPlugins;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Plugin.SharedCommands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Flow.Launcher.Plugin.PluginsManager
{
    internal class PluginsManager
    {
        const string zip = "zip";

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
            Context = context;
            Settings = settings;
        }

        internal List<Result> GetDefaultHotKeys()
        {
            return new List<Result>()
            {
                new Result()
                {
                    Title = Settings.InstallCommand,
                    IcoPath = icoPath,
                    AutoCompleteText = $"{Context.CurrentPluginMetadata.ActionKeyword} {Settings.InstallCommand} ",
                    Action = _ =>
                    {
                        Context.API.ChangeQuery(
                            $"{Context.CurrentPluginMetadata.ActionKeyword} {Settings.InstallCommand} ");
                        return false;
                    }
                },
                new Result()
                {
                    Title = Settings.UninstallCommand,
                    IcoPath = icoPath,
                    AutoCompleteText = $"{Context.CurrentPluginMetadata.ActionKeyword} {Settings.UninstallCommand} ",
                    Action = _ =>
                    {
                        Context.API.ChangeQuery(
                            $"{Context.CurrentPluginMetadata.ActionKeyword} {Settings.UninstallCommand} ");
                        return false;
                    }
                },
                new Result()
                {
                    Title = Settings.UpdateCommand,
                    IcoPath = icoPath,
                    AutoCompleteText = $"{Context.CurrentPluginMetadata.ActionKeyword} {Settings.UpdateCommand} ",
                    Action = _ =>
                    {
                        Context.API.ChangeQuery(
                            $"{Context.CurrentPluginMetadata.ActionKeyword} {Settings.UpdateCommand} ");
                        return false;
                    }
                }
            };
        }

        internal async Task InstallOrUpdateAsync(UserPlugin plugin)
        {
            if (PluginExists(plugin.ID))
            {
                if (Context.API.GetAllPlugins()
                    .Any(x => x.Metadata.ID == plugin.ID && x.Metadata.Version.CompareTo(plugin.Version) < 0))
                {
                    var updateDetail = !plugin.IsFromLocalInstallPath ? plugin.Name : plugin.LocalInstallPath;

                    Context
                        .API
                        .ChangeQuery(
                            $"{Context.CurrentPluginMetadata.ActionKeywords.FirstOrDefault()} {Settings.UpdateCommand} {updateDetail}");

                    var mainWindow = Application.Current.MainWindow;
                    mainWindow.Show();
                    mainWindow.Focus();

                    shouldHideWindow = false;

                    return;
                }

                Context.API.ShowMsg(Context.API.GetTranslation("plugin_pluginsmanager_update_alreadyexists"));
                return;
            }

            string message;
            if (Settings.AutoRestartAfterChanging)
            {
                message = string.Format(Context.API.GetTranslation("plugin_pluginsmanager_install_prompt"),
                    plugin.Name, plugin.Author,
                    Environment.NewLine, Environment.NewLine);
            }
            else
            {
                message = string.Format(Context.API.GetTranslation("plugin_pluginsmanager_install_prompt_no_restart"),
                    plugin.Name, plugin.Author,
                    Environment.NewLine);
            }

            if (MessageBox.Show(message, Context.API.GetTranslation("plugin_pluginsmanager_install_title"),
                    MessageBoxButton.YesNo) == MessageBoxResult.No)
                return;

            // at minimum should provide a name, but handle plugin that is not downloaded from plugins manifest and is a url download
            var downloadFilename = string.IsNullOrEmpty(plugin.Version)
                ? $"{plugin.Name}-{Guid.NewGuid()}.zip"
                : $"{plugin.Name}-{plugin.Version}.zip";

            var filePath = Path.Combine(Path.GetTempPath(), downloadFilename);

            try
            {
                if (!plugin.IsFromLocalInstallPath)
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);

                    await Http.DownloadAsync(plugin.UrlDownload, filePath).ConfigureAwait(false);
                }
                else
                {
                    filePath = plugin.LocalInstallPath;
                }

                Install(plugin, filePath);
            }
            catch (HttpRequestException e)
            {
                Context.API.ShowMsgError(
                    string.Format(Context.API.GetTranslation("plugin_pluginsmanager_downloading_plugin"), plugin.Name),
                    Context.API.GetTranslation("plugin_pluginsmanager_download_error"));
                Log.Exception("PluginsManager", "An error occurred while downloading plugin", e);
                return;
            }
            catch (Exception e)
            {
                Context.API.ShowMsgError(Context.API.GetTranslation("plugin_pluginsmanager_install_error_title"),
                    string.Format(Context.API.GetTranslation("plugin_pluginsmanager_install_error_subtitle"),
                        plugin.Name));
                Log.Exception("PluginsManager", "An error occurred while downloading plugin", e);
                return;
            }

            if (Settings.AutoRestartAfterChanging)
            {
                Context.API.ShowMsg(Context.API.GetTranslation("plugin_pluginsmanager_installing_plugin"),
                    string.Format(Context.API.GetTranslation("plugin_pluginsmanager_install_success_restart"),
                        plugin.Name));
                Context.API.RestartApp();
            }
            else
            {
                Context.API.ShowMsg(Context.API.GetTranslation("plugin_pluginsmanager_installing_plugin"),
                    string.Format(Context.API.GetTranslation("plugin_pluginsmanager_install_success_no_restart"),
                        plugin.Name));
            }
        }

        internal async ValueTask<List<Result>> RequestUpdateAsync(string search, CancellationToken token,
            bool usePrimaryUrlOnly = false)
        {
            await PluginsManifest.UpdateManifestAsync(token, usePrimaryUrlOnly);

            var pluginFromLocalPath = null as UserPlugin;
            var updateFromLocalPath = false;

            if (FilesFolders.IsZipFilePath(search, checkFileExists: true))
            {
                pluginFromLocalPath = Utilities.GetPluginInfoFromZip(search);
                pluginFromLocalPath.LocalInstallPath = search;
                updateFromLocalPath = true;
            }                

            var updateSource = !updateFromLocalPath
                                ? PluginsManifest.UserPlugins 
                                : new List<UserPlugin> { pluginFromLocalPath };

            var resultsForUpdate = (
                from existingPlugin in Context.API.GetAllPlugins()
                join pluginUpdateSource in updateSource
                    on existingPlugin.Metadata.ID equals pluginUpdateSource.ID
                where string.Compare(existingPlugin.Metadata.Version, pluginUpdateSource.Version,
                          StringComparison.InvariantCulture) <
                      0 // if current version precedes version of the plugin from update source (e.g. PluginsManifest)
                      && !PluginManager.PluginModified(existingPlugin.Metadata.ID)
                select
                    new
                    {
                        pluginUpdateSource.Name,
                        pluginUpdateSource.Author,
                        CurrentVersion = existingPlugin.Metadata.Version,
                        NewVersion = pluginUpdateSource.Version,
                        existingPlugin.Metadata.IcoPath,
                        PluginExistingMetadata = existingPlugin.Metadata,
                        PluginNewUserPlugin = pluginUpdateSource
                    }).ToList();

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
                            string message;
                            if (Settings.AutoRestartAfterChanging)
                            {
                                message = string.Format(
                                    Context.API.GetTranslation("plugin_pluginsmanager_update_prompt"),
                                    x.Name, x.Author,
                                    Environment.NewLine, Environment.NewLine);
                            }
                            else
                            {
                                message = string.Format(
                                    Context.API.GetTranslation("plugin_pluginsmanager_update_prompt_no_restart"),
                                    x.Name, x.Author,
                                    Environment.NewLine);
                            }

                            if (MessageBox.Show(message,
                                    Context.API.GetTranslation("plugin_pluginsmanager_update_title"),
                                    MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                            {
                                return false;
                            }

                            var downloadToFilePath = Path.Combine(Path.GetTempPath(),
                                $"{x.Name}-{x.NewVersion}.zip");

                            _ = Task.Run(async delegate
                            {
                                if (!x.PluginNewUserPlugin.IsFromLocalInstallPath)
                                {
                                    if (File.Exists(downloadToFilePath))
                                    {
                                        File.Delete(downloadToFilePath);
                                    }

                                    await Http.DownloadAsync(x.PluginNewUserPlugin.UrlDownload, downloadToFilePath)
                                        .ConfigureAwait(false);
                                }
                                else
                                {
                                    downloadToFilePath = x.PluginNewUserPlugin.LocalInstallPath;
                                }
                                

                                PluginManager.UpdatePlugin(x.PluginExistingMetadata, x.PluginNewUserPlugin,
                                    downloadToFilePath);

                                if (Settings.AutoRestartAfterChanging)
                                {
                                    Context.API.ShowMsg(
                                        Context.API.GetTranslation("plugin_pluginsmanager_update_title"),
                                        string.Format(
                                            Context.API.GetTranslation(
                                                "plugin_pluginsmanager_update_success_restart"),
                                            x.Name));
                                    Context.API.RestartApp();
                                }
                                else
                                {
                                    Context.API.ShowMsg(
                                        Context.API.GetTranslation("plugin_pluginsmanager_update_title"),
                                        string.Format(
                                            Context.API.GetTranslation(
                                                "plugin_pluginsmanager_update_success_no_restart"),
                                            x.Name));
                                }
                            }).ContinueWith(t =>
                            {
                                Log.Exception("PluginsManager", $"Update failed for {x.Name}",
                                    t.Exception.InnerException);
                                Context.API.ShowMsg(
                                    Context.API.GetTranslation("plugin_pluginsmanager_install_error_title"),
                                    string.Format(
                                        Context.API.GetTranslation("plugin_pluginsmanager_install_error_subtitle"),
                                        x.Name));
                            }, TaskContinuationOptions.OnlyOnFaulted);

                            return true;

                        },
                        ContextData =
                            new UserPlugin
                            {
                                Website = x.PluginNewUserPlugin.Website,
                                UrlSourceCode = x.PluginNewUserPlugin.UrlSourceCode
                            }
                    });

            // Update all result
            if (resultsForUpdate.Count() > 1)
            {
                var updateAllResult = new Result
                {
                    Title = Context.API.GetTranslation("plugin_pluginsmanager_update_all_title"),
                    SubTitle = Context.API.GetTranslation("plugin_pluginsmanager_update_all_subtitle"),
                    IcoPath = icoPath,
                    AsyncAction = async e =>
                    {
                        string message;
                        if (Settings.AutoRestartAfterChanging)
                        {
                            message = string.Format(
                                Context.API.GetTranslation("plugin_pluginsmanager_update_all_prompt"),
                                resultsForUpdate.Count(), Environment.NewLine);
                        }
                        else
                        {
                            message = string.Format(
                                Context.API.GetTranslation("plugin_pluginsmanager_update_all_prompt_no_restart"),
                                resultsForUpdate.Count());
                        }

                        if (MessageBox.Show(message,
                                Context.API.GetTranslation("plugin_pluginsmanager_update_title"),
                                MessageBoxButton.YesNo) == MessageBoxResult.No)
                        {
                            return false;
                        }

                        await Task.WhenAll(resultsForUpdate.Select(async plugin =>
                        {
                            var downloadToFilePath = Path.Combine(Path.GetTempPath(),
                                $"{plugin.Name}-{plugin.NewVersion}.zip");

                            try
                            {
                                if (File.Exists(downloadToFilePath))
                                {
                                    File.Delete(downloadToFilePath);
                                }

                                await Http.DownloadAsync(plugin.PluginNewUserPlugin.UrlDownload, downloadToFilePath)
                                    .ConfigureAwait(false);

                                PluginManager.UpdatePlugin(plugin.PluginExistingMetadata, plugin.PluginNewUserPlugin,
                                    downloadToFilePath);
                            }
                            catch (Exception ex)
                            {
                                Log.Exception("PluginsManager", $"Update failed for {plugin.Name}", ex.InnerException);
                                Context.API.ShowMsg(
                                    Context.API.GetTranslation("plugin_pluginsmanager_install_error_title"),
                                    string.Format(
                                        Context.API.GetTranslation("plugin_pluginsmanager_install_error_subtitle"),
                                        plugin.Name));
                            }
                        }));

                        if (Settings.AutoRestartAfterChanging)
                        {
                            Context.API.ShowMsg(Context.API.GetTranslation("plugin_pluginsmanager_update_title"),
                                string.Format(
                                    Context.API.GetTranslation("plugin_pluginsmanager_update_all_success_restart"),
                                    resultsForUpdate.Count()));
                            Context.API.RestartApp();
                        }
                        else
                        {
                            Context.API.ShowMsg(Context.API.GetTranslation("plugin_pluginsmanager_update_title"),
                                string.Format(
                                    Context.API.GetTranslation("plugin_pluginsmanager_update_all_success_no_restart"),
                                    resultsForUpdate.Count()));
                        }

                        return true;
                    },
                    ContextData = new UserPlugin()
                };
                results = results.Prepend(updateAllResult);
            }

            return !updateFromLocalPath ? Search(results, search) : results.ToList();
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

        internal List<Result> InstallFromWeb(string url)
        {
            var filename = url.Split("/").Last();
            var name = filename.Split(string.Format(".{0}", zip)).First();

            var plugin = new UserPlugin
            {
                ID = "",
                Name = name,
                Version = string.Empty,
                Author = Context.API.GetTranslation("plugin_pluginsmanager_unknown_author"),
                UrlDownload = url
            };

            var result = new Result
            {
                Title = string.Format(Context.API.GetTranslation("plugin_pluginsmanager_install_from_web"), filename),
                SubTitle = plugin.UrlDownload,
                IcoPath = icoPath,
                Action = e =>
                {
                    if (e.SpecialKeyState.CtrlPressed)
                    {
                        SearchWeb.OpenInBrowserTab(plugin.UrlDownload);
                        return ShouldHideWindow;
                    }

                    if (Settings.WarnFromUnknownSource)
                    {
                        if (!InstallSourceKnown(plugin.UrlDownload)
                            && MessageBox.Show(string.Format(
                                    Context.API.GetTranslation("plugin_pluginsmanager_install_unknown_source_warning"),
                                    Environment.NewLine),
                                Context.API.GetTranslation(
                                    "plugin_pluginsmanager_install_unknown_source_warning_title"),
                                MessageBoxButton.YesNo) == MessageBoxResult.No)
                            return false;
                    }

                    Application.Current.MainWindow.Hide();
                    _ = InstallOrUpdateAsync(plugin);

                    return ShouldHideWindow;
                }
            };

            return new List<Result> { result };
        }

        internal List<Result> InstallFromLocalPath(string localPath)
        {
            var plugin = Utilities.GetPluginInfoFromZip(localPath);

            plugin.LocalInstallPath = localPath;

            return new List<Result>
            {
                new Result
                {
                    Title = $"{plugin.Name} by {plugin.Author}",
                    SubTitle = plugin.Description,
                    IcoPath = plugin.IcoPath,
                    Action = e =>
                    {
                        if (Settings.WarnFromUnknownSource)
                        {
                            if (!InstallSourceKnown(plugin.Website)
                                && MessageBox.Show(string.Format(
                                        Context.API.GetTranslation("plugin_pluginsmanager_install_unknown_source_warning"),
                                        Environment.NewLine),
                                    Context.API.GetTranslation(
                                        "plugin_pluginsmanager_install_unknown_source_warning_title"),
                                    MessageBoxButton.YesNo) == MessageBoxResult.No)
                                return false;
                        }

                        Application.Current.MainWindow.Hide();
                        _ = InstallOrUpdateAsync(plugin);

                        return ShouldHideWindow;
                    }
                }
            };
        }

        private bool InstallSourceKnown(string url)
        {
            var author = url.Split('/')[3];
            var acceptedSource = "https://github.com";
            var constructedUrlPart = string.Format("{0}/{1}/", acceptedSource, author);

            return url.StartsWith(acceptedSource) &&
                   Context.API.GetAllPlugins().Any(x => x.Metadata.Website.StartsWith(constructedUrlPart));
        }

        internal async ValueTask<List<Result>> RequestInstallOrUpdate(string search, CancellationToken token,
            bool usePrimaryUrlOnly = false)
        {
            await PluginsManifest.UpdateManifestAsync(token, usePrimaryUrlOnly);

            if (Uri.IsWellFormedUriString(search, UriKind.Absolute)
                && search.Split('.').Last() == zip)
                return InstallFromWeb(search);

            if (FilesFolders.IsZipFilePath(search, checkFileExists: true))
                return InstallFromLocalPath(search);

            var results =
                PluginsManifest
                    .UserPlugins
                    .Where(x => !PluginExists(x.ID) && !PluginManager.PluginModified(x.ID))
                    .Select(x =>
                        new Result
                        {
                            Title = $"{x.Name} by {x.Author}",
                            SubTitle = x.Description,
                            IcoPath = x.IcoPath,
                            Action = e =>
                            {
                                if (e.SpecialKeyState.CtrlPressed)
                                {
                                    SearchWeb.OpenInBrowserTab(x.Website);
                                    return ShouldHideWindow;
                                }

                                Application.Current.MainWindow.Hide();
                                _ = InstallOrUpdateAsync(x); // No need to wait
                                return ShouldHideWindow;
                            },
                            ContextData = x
                        });

            return Search(results, search);
        }

        private void Install(UserPlugin plugin, string downloadedFilePath)
        {
            if (!File.Exists(downloadedFilePath))
                throw new FileNotFoundException($"Plugin {plugin.ID} zip file not found at {downloadedFilePath}",
                    downloadedFilePath);

            try
            {
                PluginManager.InstallPlugin(plugin, downloadedFilePath);
                
                if (!plugin.IsFromLocalInstallPath)
                    File.Delete(downloadedFilePath);
            }
            catch (FileNotFoundException e)
            {
                Context.API.ShowMsgError(Context.API.GetTranslation("plugin_pluginsmanager_install_error_title"),
                    Context.API.GetTranslation("plugin_pluginsmanager_install_errormetadatafile"));
                Log.Exception("Flow.Launcher.Plugin.PluginsManager", e.Message, e);
            }
            catch (InvalidOperationException e)
            {
                Context.API.ShowMsgError(Context.API.GetTranslation("plugin_pluginsmanager_install_error_title"),
                    string.Format(Context.API.GetTranslation("plugin_pluginsmanager_install_error_duplicate"),
                        plugin.Name));
                Log.Exception("Flow.Launcher.Plugin.PluginsManager", e.Message, e);
            }
            catch (ArgumentException e)
            {
                Context.API.ShowMsgError(Context.API.GetTranslation("plugin_pluginsmanager_install_error_title"),
                    string.Format(Context.API.GetTranslation("plugin_pluginsmanager_plugin_modified_error"),
                        plugin.Name));
                Log.Exception("Flow.Launcher.Plugin.PluginsManager", e.Message, e);
            }
        }

        internal List<Result> RequestUninstall(string search)
        {
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
                            string message;
                            if (Settings.AutoRestartAfterChanging)
                            {
                                message = string.Format(
                                    Context.API.GetTranslation("plugin_pluginsmanager_uninstall_prompt"),
                                    x.Metadata.Name, x.Metadata.Author,
                                    Environment.NewLine, Environment.NewLine);
                            }
                            else
                            {
                                message = string.Format(
                                    Context.API.GetTranslation("plugin_pluginsmanager_uninstall_prompt_no_restart"),
                                    x.Metadata.Name, x.Metadata.Author,
                                    Environment.NewLine);
                            }

                            if (MessageBox.Show(message,
                                    Context.API.GetTranslation("plugin_pluginsmanager_uninstall_title"),
                                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                            {
                                Application.Current.MainWindow.Hide();
                                Uninstall(x.Metadata);
                                if (Settings.AutoRestartAfterChanging)
                                {
                                    Context.API.RestartApp();
                                }
                                else
                                {
                                    Context.API.ShowMsg(
                                        Context.API.GetTranslation("plugin_pluginsmanager_uninstall_title"),
                                        string.Format(
                                            Context.API.GetTranslation(
                                                "plugin_pluginsmanager_uninstall_success_no_restart"),
                                            x.Metadata.Name));
                                }

                                return true;
                            }

                            return false;
                        }
                    });

            return Search(results, search);
        }

        private void Uninstall(PluginMetadata plugin)
        {
            try
            {
                PluginManager.UninstallPlugin(plugin, removeSettings: true);
            }
            catch (ArgumentException e)
            {
                Log.Exception("Flow.Launcher.Plugin.PluginsManager", e.Message, e);
                Context.API.ShowMsgError(Context.API.GetTranslation("plugin_pluginsmanager_uninstall_error_title"),
                    Context.API.GetTranslation("plugin_pluginsmanager_plugin_modified_error"));
            }
        }
    }
}
