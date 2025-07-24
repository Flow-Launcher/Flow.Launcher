using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using Flow.Launcher.Plugin.Explorer.Helper;
using Flow.Launcher.Plugin.Explorer.ViewModels;
using Flow.Launcher.Plugin.Explorer.Views;
using System.Windows.Controls;

namespace Flow.Launcher.Plugin.Explorer
{
    internal class ContextMenu : IContextMenu
    {
        private static readonly string ClassName = nameof(ContextMenu);

        private PluginInitContext Context { get; set; }

        private Settings Settings { get; set; }

        private SettingsViewModel ViewModel { get; set; }

        public ContextMenu(PluginInitContext context, Settings settings, SettingsViewModel vm)
        {
            Context = context;
            Settings = settings;
            ViewModel = vm;
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var contextMenus = new List<Result>();
            if (selectedResult.ContextData is SearchResult record)
            {
                if (record.Type == ResultType.File && !string.IsNullOrEmpty(Settings.EditorPath))
                    contextMenus.Add(CreateOpenWithEditorResult(record, Settings.EditorPath));

                if ((record.Type == ResultType.Folder || record.Type == ResultType.Volume) && !string.IsNullOrEmpty(Settings.FolderEditorPath))
                    contextMenus.Add(CreateOpenWithEditorResult(record, Settings.FolderEditorPath));

                if (record.Type == ResultType.Folder)
                {
                    contextMenus.Add(CreateOpenWithShellResult(record));
                    if (record.WindowsIndexed)
                    {
                        contextMenus.Add(CreateAddToIndexSearchExclusionListResult(record));
                    }
                }

                contextMenus.Add(CreateOpenContainingFolderResult(record));

                if (record.Type == ResultType.File)
                {
                    contextMenus.Add(CreateOpenWithMenu(record));
                }

                if (record.WindowsIndexed)
                {
                    contextMenus.Add(CreateOpenWindowsIndexingOptions());
                }

                var icoPath = (record.Type == ResultType.File) ? Constants.FileImagePath : Constants.FolderImagePath;
                bool isFile = record.Type == ResultType.File;

                if (Settings.QuickAccessLinks.All(x => !x.Path.Equals(record.FullPath, StringComparison.OrdinalIgnoreCase)))
                {
                    contextMenus.Add(new Result
                    {
                        Title = Context.API.GetTranslation("plugin_explorer_add_to_quickaccess_title"),
                        SubTitle = Context.API.GetTranslation("plugin_explorer_add_to_quickaccess_subtitle"),
                        Action = (context) =>
                        {
                            Settings.QuickAccessLinks.Add(new AccessLink
                            {
                                Name = record.FullPath.GetPathName(),
                                Path = record.FullPath,
                                Type = record.Type
                            });

                            Context.API.ShowMsg(Context.API.GetTranslation("plugin_explorer_addfilefoldersuccess"),
                                    Context.API.GetTranslation("plugin_explorer_addfilefoldersuccess_detail"),
                                    Constants.ExplorerIconImageFullPath);

                            ViewModel.Save();

                            return true;
                        },
                        SubTitleToolTip = Context.API.GetTranslation("plugin_explorer_contextmenu_titletooltip"),
                        TitleToolTip = Context.API.GetTranslation("plugin_explorer_contextmenu_titletooltip"),
                        IcoPath = Constants.QuickAccessImagePath,
                        Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue718"),
                    });
                }
                else
                {
                    contextMenus.Add(new Result
                    {
                        Title = Context.API.GetTranslation("plugin_explorer_remove_from_quickaccess_title"),
                        SubTitle = Context.API.GetTranslation("plugin_explorer_remove_from_quickaccess_subtitle"),
                        Action = (context) =>
                        {
                            Settings.QuickAccessLinks.Remove(Settings.QuickAccessLinks.FirstOrDefault(x => string.Equals(x.Path, record.FullPath, StringComparison.OrdinalIgnoreCase)));

                            Context.API.ShowMsg(Context.API.GetTranslation("plugin_explorer_removefilefoldersuccess"),
                                    Context.API.GetTranslation("plugin_explorer_removefilefoldersuccess_detail"),
                                    Constants.ExplorerIconImageFullPath);

                            ViewModel.Save();

                            return true;
                        },
                        SubTitleToolTip = Context.API.GetTranslation("plugin_explorer_contextmenu_remove_titletooltip"),
                        TitleToolTip = Context.API.GetTranslation("plugin_explorer_contextmenu_remove_titletooltip"),
                        IcoPath = Constants.RemoveQuickAccessImagePath,
                        Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\uecc9")
                    });
                }

                contextMenus.Add(new Result
                {
                    Title = Context.API.GetTranslation("plugin_explorer_copypath"),
                    SubTitle = Context.API.GetTranslation("plugin_explorer_copypath_subtitle"),
                    Action = _ =>
                    {
                        try
                        {
                            Context.API.CopyToClipboard(record.FullPath);
                            return true;
                        }
                        catch (Exception e)
                        {
                            LogException("Fail to set text in clipboard", e);
                            Context.API.ShowMsgError(Context.API.GetTranslation("plugin_explorer_fail_to_set_text"));
                            return false;
                        }
                    },
                    IcoPath = Constants.CopyImagePath,
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue8c8")
                });

                contextMenus.Add(new Result
                {
                    Title = Context.API.GetTranslation("plugin_explorer_copyname"),
                    SubTitle = Context.API.GetTranslation("plugin_explorer_copyname_subtitle"),
                    Action = _ =>
                    {
                        try
                        {
                            Context.API.CopyToClipboard(Path.GetFileName(record.FullPath));
                            return true;
                        }
                        catch (Exception e)
                        {
                            LogException("Fail to set text in clipboard", e);
                            Context.API.ShowMsgError(Context.API.GetTranslation("plugin_explorer_fail_to_set_text"));
                            return false;
                        }
                    },
                    IcoPath = Constants.CopyImagePath,
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue8c8")
                });

                contextMenus.Add(new Result
                {
                    Title = Context.API.GetTranslation("plugin_explorer_copyfilefolder"),
                    SubTitle = isFile ? Context.API.GetTranslation("plugin_explorer_copyfile_subtitle") : Context.API.GetTranslation("plugin_explorer_copyfolder_subtitle"),
                    Action = _ =>
                    {
                        try
                        {
                            Context.API.CopyToClipboard(record.FullPath, directCopy: true);
                            return true;
                        }
                        catch (Exception e)
                        {
                            LogException($"Fail to set file/folder in clipboard", e);
                            Context.API.ShowMsgError(Context.API.GetTranslation("plugin_explorer_fail_to_set_files"));
                            return false;
                        }

                    },
                    IcoPath = icoPath,
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\uf12b")
                });
                contextMenus.Add(new Result
                {
                    Title = Context.API.GetTranslation("plugin_explorer_rename_a_file"),
                    SubTitle = Context.API.GetTranslation("plugin_explorer_rename_subtitle"),
                    Action = _ =>
                    {
                        RenameFile window;
                        switch (record.Type)
                        {
                            case ResultType.Folder:
                                window = new RenameFile(Context.API, new DirectoryInfo(record.FullPath));
                                break;
                            case ResultType.File:
                                window = new RenameFile(Context.API, new FileInfo(record.FullPath));
                                break;
                            default:
                                Context.API.ShowMsgError(Context.API.GetTranslation("plugin_explorer_cannot_rename"));
                                return false;
                        }
                        window.ShowDialog();

                        return false;

                    },
                    // placeholder until real image is found
                    IcoPath = Constants.RenameImagePath,
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue8ac")

                });


                if (record.Type is ResultType.File or ResultType.Folder)
                    contextMenus.Add(new Result
                    {
                        Title = Context.API.GetTranslation("plugin_explorer_deletefilefolder"),
                        SubTitle = isFile ? Context.API.GetTranslation("plugin_explorer_deletefile_subtitle") : Context.API.GetTranslation("plugin_explorer_deletefolder_subtitle"),
                        Action = (context) =>
                        {
                            try
                            {
                                if (Context.API.ShowMsgBox(
                                        string.Format(Context.API.GetTranslation("plugin_explorer_delete_folder_link"), record.FullPath),
                                        Context.API.GetTranslation("plugin_explorer_deletefilefolder"),
                                        MessageBoxButton.OKCancel,
                                        MessageBoxImage.Warning)
                                    == MessageBoxResult.Cancel)
                                    return false;

                                if (isFile)
                                    File.Delete(record.FullPath);
                                else
                                    Directory.Delete(record.FullPath, true);

                                _ = Task.Run(() =>
                                {
                                    Context.API.ShowMsg(Context.API.GetTranslation("plugin_explorer_deletefilefoldersuccess"),
                                        string.Format(Context.API.GetTranslation("plugin_explorer_deletefilefoldersuccess_detail"), record.FullPath),
                                        Constants.ExplorerIconImageFullPath);
                                });
                            }
                            catch (Exception e)
                            {
                                LogException($"Fail to delete {record.FullPath}", e);
                                Context.API.ShowMsgError(string.Format(Context.API.GetTranslation("plugin_explorer_fail_to_delete"), record.FullPath));
                                return false;
                            }

                            return true;
                        },
                        IcoPath = Constants.DeleteFileFolderImagePath,
                        Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue74d")
                    });

                if (record.Type is not ResultType.Volume)
                {
                    contextMenus.Add(new Result()
                    {
                        Title = Context.API.GetTranslation("plugin_explorer_show_contextmenu_title"),
                        IcoPath = Constants.ShowContextMenuImagePath,
                        Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue700"),
                        Action = _ =>
                        {
                            if (record.Type is ResultType.Volume)
                                return false;

                            ResultManager.ShowNativeContextMenu(record.FullPath, record.Type);

                            return false;
                        },
                    });
                }

                if (record.Type == ResultType.File && CanRunAsDifferentUser(record.FullPath))
                    contextMenus.Add(new Result
                    {
                        Title = Context.API.GetTranslation("plugin_explorer_runasdifferentuser"),
                        SubTitle = Context.API.GetTranslation("plugin_explorer_runasdifferentuser_subtitle"),
                        Action = (context) =>
                        {
                            try
                            {
                                _ = Task.Run(() => ShellCommand.RunAsDifferentUser(record.FullPath.SetProcessStartInfo()));
                            }
                            catch (FileNotFoundException e)
                            {
                                Context.API.ShowMsgError(
                                    Context.API.GetTranslation("plugin_explorer_plugin_name"),
                                    string.Format(Context.API.GetTranslation("plugin_explorer_file_not_found"), e.Message));
                                return false;
                            }

                            return true;
                        },
                        IcoPath = Constants.DifferentUserIconImagePath,
                        Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue748"),
                    });

                if (record.Type is ResultType.File or ResultType.Folder && Settings.ShowInlinedWindowsContextMenu)
                {
                    var includedItems = Settings
                        .WindowsContextMenuIncludedItems
                        .Replace("\r", "")
                        .Split("\n")
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .ToArray();
                    var excludedItems = Settings
                        .WindowsContextMenuExcludedItems
                        .Replace("\r", "")
                        .Split("\n")
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .ToArray();
                    var menuItems = ShellContextMenuDisplayHelper
                        .GetContextMenuWithIcons(record.FullPath)
                        .Where(contextMenuItem =>
                            (includedItems.Length == 0 || includedItems.Any(filter =>
                                contextMenuItem.Label.Contains(filter, StringComparison.OrdinalIgnoreCase)
                            )) &&
                            (excludedItems.Length == 0 || !excludedItems.Any(filter =>
                                contextMenuItem.Label.Contains(filter, StringComparison.OrdinalIgnoreCase)
                            ))
                        );
                    foreach (var menuItem in menuItems)
                    {
                        contextMenus.Add(new Result
                        {
                            Title = menuItem.Label,
                            Icon = () => menuItem.Icon,
                            Action = _ =>
                            {
                                ShellContextMenuDisplayHelper.ExecuteContextMenuItem(record.FullPath, menuItem.CommandId);
                                return true;
                            }
                        });
                    }
                }
            }

            return contextMenus;
        }

        private Result CreateOpenContainingFolderResult(SearchResult record)
        {
            return new Result
            {
                Title = Context.API.GetTranslation("plugin_explorer_opencontainingfolder"),
                SubTitle = Context.API.GetTranslation("plugin_explorer_opencontainingfolder_subtitle"),
                Action = _ =>
                {
                    try
                    {
                        Context.API.OpenDirectory(Path.GetDirectoryName(record.FullPath), record.FullPath);
                    }
                    catch (Exception e)
                    {
                        LogException($"Fail to open file at {record.FullPath}", e);
                        Context.API.ShowMsgError(string.Format(Context.API.GetTranslation("plugin_explorer_fail_to_open"), record.FullPath));
                        return false;
                    }

                    return true;
                },
                IcoPath = Constants.FolderImagePath,
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue838")
            };
        }



        private Result CreateOpenWithEditorResult(SearchResult record, string editorPath)
        {
            var name = $"{Context.API.GetTranslation("plugin_explorer_openwitheditor")} {Path.GetFileNameWithoutExtension(editorPath)}";

            return new Result
            {
                Title = name,
                Action = _ =>
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo()
                        {
                            FileName = editorPath,
                            ArgumentList = { record.FullPath }
                        });
                        return true;
                    }
                    catch (Exception e)
                    {
                        var raw_message = Context.API.GetTranslation("plugin_explorer_openwitheditor_error");
                        var message = string.Format(raw_message, record.FullPath, Path.GetFileNameWithoutExtension(editorPath), editorPath);
                        LogException(message, e);
                        Context.API.ShowMsgError(message);
                        return false;
                    }
                },
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue70f"),
                IcoPath = Constants.FileImagePath
            };
        }

        private Result CreateOpenWithShellResult(SearchResult record)
        {
            string shellPath = Settings.ShellPath;

            var name = $"{Context.API.GetTranslation("plugin_explorer_openwithshell")} {Path.GetFileNameWithoutExtension(shellPath)}";

            return new Result
            {
                Title = name,
                Action = _ =>
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo()
                        {
                            FileName = shellPath, WorkingDirectory = record.FullPath
                        });
                        return true;
                    }
                    catch (Exception e)
                    {
                        var raw_message = Context.API.GetTranslation("plugin_explorer_openwithshell_error");
                        var message = string.Format(raw_message, record.FullPath, Path.GetFileNameWithoutExtension(shellPath), shellPath);
                        LogException(message, e);
                        Context.API.ShowMsgError(message);
                        return false;
                    }
                },
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue756"),
                IcoPath = Constants.FolderImagePath
            };
        }

        private Result CreateAddToIndexSearchExclusionListResult(SearchResult record)
        {
            return new Result
            {
                Title = Context.API.GetTranslation("plugin_explorer_excludefromindexsearch"),
                SubTitle = Context.API.GetTranslation("plugin_explorer_path") + " " + record.FullPath,
                Action = c_ =>
                {
                    if (!Settings.IndexSearchExcludedSubdirectoryPaths.Any(x => string.Equals(x.Path, record.FullPath, StringComparison.OrdinalIgnoreCase)))
                        Settings.IndexSearchExcludedSubdirectoryPaths.Add(new AccessLink
                        {
                            Path = record.FullPath
                        });

                    _ = Task.Run(() =>
                    {
                        Context.API.ShowMsg(Context.API.GetTranslation("plugin_explorer_excludedfromindexsearch_msg"),
                            Context.API.GetTranslation("plugin_explorer_path") +
                            " " + record.FullPath, Constants.ExplorerIconImageFullPath);

                        // so the new path can be persisted to storage and not wait till next ViewModel save.
                        Context.API.SaveAppAllSettings();
                    });

                    return false;
                },
                IcoPath = Constants.ExcludeFromIndexImagePath,
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\uf140"),
            };
        }

        private Result CreateOpenWindowsIndexingOptions()
        {
            return new Result
            {
                Title = Context.API.GetTranslation("plugin_explorer_openindexingoptions"),
                SubTitle = Context.API.GetTranslation("plugin_explorer_openindexingoptions_subtitle"),
                Action = _ =>
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "control.exe",
                            UseShellExecute = true,
                            Arguments = "srchadmin.dll"
                        };

                        Process.Start(psi);
                        return true;
                    }
                    catch (Exception e)
                    {
                        var message = Context.API.GetTranslation("plugin_explorer_openindexingoptions_errormsg");
                        LogException(message, e);
                        Context.API.ShowMsgError(message);
                        return false;
                    }
                },
                IcoPath = Constants.IndexingOptionsIconImagePath,
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue773"),
            };
        }

        private Result CreateOpenWithMenu(SearchResult record)
        {
            return new Result
            {
                Title = Context.API.GetTranslation("plugin_explorer_openwith"),
                SubTitle = Context.API.GetTranslation("plugin_explorer_openwith_subtitle"),
                Action = _ =>
                {
                    Process.Start("rundll32.exe", $"{Path.Combine(Environment.SystemDirectory, "shell32.dll")},OpenAs_RunDLL {record.FullPath}");
                    return true;
                },
                IcoPath = Constants.ShowContextMenuImagePath,
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue7ac"),
            };
        }

        private void LogException(string message, Exception e)
        {
            Context.API.LogException(ClassName, message, e);
        }

        private static bool CanRunAsDifferentUser(string path)
        {
            return Path.GetExtension(path) switch
            {
                ".exe" or ".bat" or ".msi" => true,
                _ => false,
            };
        }
    }
}
