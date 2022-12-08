﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using MessageBox = System.Windows.Forms.MessageBox;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;
using MessageBoxButton = System.Windows.Forms.MessageBoxButtons;
using DialogResult = System.Windows.Forms.DialogResult;
using Flow.Launcher.Plugin.Explorer.ViewModels;

namespace Flow.Launcher.Plugin.Explorer
{
    internal class ContextMenu : IContextMenu
    {
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
                    contextMenus.Add(CreateOpenWithEditorResult(record));

                if (record.Type == ResultType.Folder && record.WindowsIndexed)
                {
                    contextMenus.Add(CreateAddToIndexSearchExclusionListResult(record));
                    contextMenus.Add(CreateOpenWithShellResult(record));
                }
                contextMenus.Add(CreateOpenContainingFolderResult(record));

                if (record.WindowsIndexed)
                {
                    contextMenus.Add(CreateOpenWindowsIndexingOptions());
                }

                var icoPath = (record.Type == ResultType.File) ? Constants.FileImagePath : Constants.FolderImagePath;
                var fileOrFolder = (record.Type == ResultType.File) ? "file" : "folder";

                if (Settings.QuickAccessLinks.All(x => x.Path != record.FullPath))
                {
                    contextMenus.Add(new Result
                    {
                        Title = Context.API.GetTranslation("plugin_explorer_add_to_quickaccess_title"),
                        SubTitle = string.Format(Context.API.GetTranslation("plugin_explorer_add_to_quickaccess_subtitle"), fileOrFolder),
                        Action = (context) =>
                        {
                            Settings.QuickAccessLinks.Add(new AccessLink
                            {
                                Path = record.FullPath, Type = record.Type
                            });

                            Context.API.ShowMsg(Context.API.GetTranslation("plugin_explorer_addfilefoldersuccess"),
                                string.Format(
                                    Context.API.GetTranslation("plugin_explorer_addfilefoldersuccess_detail"),
                                    fileOrFolder),
                                Constants.ExplorerIconImageFullPath);

                            ViewModel.Save();

                            return true;
                        },
                        SubTitleToolTip = Context.API.GetTranslation("plugin_explorer_contextmenu_titletooltip"),
                        TitleToolTip = Context.API.GetTranslation("plugin_explorer_contextmenu_titletooltip"),
                        IcoPath = Constants.QuickAccessImagePath
                    });
                }
                else
                {
                    contextMenus.Add(new Result
                    {
                        Title = Context.API.GetTranslation("plugin_explorer_remove_from_quickaccess_title"),
                        SubTitle = string.Format(Context.API.GetTranslation("plugin_explorer_remove_from_quickaccess_subtitle"), fileOrFolder),
                        Action = (context) =>
                        {
                            Settings.QuickAccessLinks.Remove(Settings.QuickAccessLinks.FirstOrDefault(x => x.Path == record.FullPath));

                            Context.API.ShowMsg(Context.API.GetTranslation("plugin_explorer_removefilefoldersuccess"),
                                string.Format(
                                    Context.API.GetTranslation("plugin_explorer_removefilefoldersuccess_detail"),
                                    fileOrFolder),
                                Constants.ExplorerIconImageFullPath);

                            ViewModel.Save();

                            return true;
                        },
                        SubTitleToolTip = Context.API.GetTranslation("plugin_explorer_contextmenu_remove_titletooltip"),
                        TitleToolTip = Context.API.GetTranslation("plugin_explorer_contextmenu_remove_titletooltip"),
                        IcoPath = Constants.RemoveQuickAccessImagePath
                    });
                }

                contextMenus.Add(new Result
                {
                    Title = Context.API.GetTranslation("plugin_explorer_copypath"),
                    SubTitle = $"Copy the current {fileOrFolder} path to clipboard",
                    Action = _ =>
                    {
                        try
                        {
                            Clipboard.SetText(record.FullPath);
                            return true;
                        }
                        catch (Exception e)
                        {
                            var message = "Fail to set text in clipboard";
                            LogException(message, e);
                            Context.API.ShowMsg(message);
                            return false;
                        }
                    },
                    IcoPath = Constants.CopyImagePath
                });

                contextMenus.Add(new Result
                {
                    Title = Context.API.GetTranslation("plugin_explorer_copyfilefolder") + $" {fileOrFolder}",
                    SubTitle = $"Copy the {fileOrFolder} to clipboard",
                    Action = _ =>
                    {
                        try
                        {
                            Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection
                            {
                                record.FullPath
                            });
                            return true;
                        }
                        catch (Exception e)
                        {
                            var message = $"Fail to set {fileOrFolder} in clipboard";
                            LogException(message, e);
                            Context.API.ShowMsg(message);
                            return false;
                        }

                    },
                    IcoPath = icoPath
                });


                if (record.Type is ResultType.File or ResultType.Folder)
                    contextMenus.Add(new Result
                    {
                        Title = Context.API.GetTranslation("plugin_explorer_deletefilefolder") + $" {fileOrFolder}",
                        SubTitle = Context.API.GetTranslation("plugin_explorer_deletefilefolder_subtitle") + $" {fileOrFolder}",
                        Action = (context) =>
                        {
                            try
                            {
                                if (MessageBox.Show(
                                        string.Format(Context.API.GetTranslation("plugin_explorer_deletefilefolderconfirm"), fileOrFolder),
                                        string.Empty,
                                        MessageBoxButton.YesNo,
                                        MessageBoxIcon.Warning)
                                    == DialogResult.No)
                                    return false;

                                if (record.Type == ResultType.File)
                                    File.Delete(record.FullPath);
                                else
                                    Directory.Delete(record.FullPath, true);

                                _ = Task.Run(() =>
                                {
                                    Context.API.ShowMsg(Context.API.GetTranslation("plugin_explorer_deletefilefoldersuccess"),
                                        string.Format(Context.API.GetTranslation("plugin_explorer_deletefilefoldersuccess_detail"), fileOrFolder),
                                        Constants.ExplorerIconImageFullPath);
                                });
                            }
                            catch (Exception e)
                            {
                                var message = $"Fail to delete {fileOrFolder} at {record.FullPath}";
                                LogException(message, e);
                                Context.API.ShowMsgError(message);
                                return false;
                            }

                            return true;
                        },
                        IcoPath = Constants.DeleteFileFolderImagePath
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

                            var screenWithMouseCursor = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
                            var xOfScreenCenter = screenWithMouseCursor.WorkingArea.Left + screenWithMouseCursor.WorkingArea.Width / 2;
                            var yOfScreenCenter = screenWithMouseCursor.WorkingArea.Top + screenWithMouseCursor.WorkingArea.Height / 2;
                            var showPosition = new System.Drawing.Point(xOfScreenCenter, yOfScreenCenter);

                            switch (record.Type)
                            {
                                case ResultType.File:
                                {
                                    var fileInfos = new FileInfo[]
                                    {
                                        new(record.FullPath)
                                    };

                                    new Peter.ShellContextMenu().ShowContextMenu(fileInfos, showPosition);
                                    break;
                                }
                                case ResultType.Folder:
                                {
                                    var directoryInfos = new DirectoryInfo[]
                                    {
                                        new(record.FullPath)
                                    };

                                    new Peter.ShellContextMenu().ShowContextMenu(directoryInfos, showPosition);
                                    break;
                                }
                            }

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
                                var name = "Plugin: Folder";
                                var message = $"File not found: {e.Message}";
                                Context.API.ShowMsgError(name, message);
                            }

                            return true;
                        },
                        IcoPath = Constants.DifferentUserIconImagePath
                    });
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
                        var message = $"Fail to open file at {record.FullPath}";
                        LogException(message, e);
                        Context.API.ShowMsgError(message);
                        return false;
                    }

                    return true;
                },
                IcoPath = Constants.FolderImagePath
            };
        }



        private Result CreateOpenWithEditorResult(SearchResult record)
        {
            string editorPath = Settings.EditorPath;

            var name = $"{Context.API.GetTranslation("plugin_explorer_openwitheditor")} {Path.GetFileNameWithoutExtension(editorPath)}";

            return new Result
            {
                Title = name,
                Action = _ =>
                {
                    try
                    {
                        Process.Start(editorPath, record.FullPath);
                        return true;
                    }
                    catch (Exception e)
                    {
                        var message = $"Failed to open editor for file at {record.FullPath} with Editor {Path.GetFileNameWithoutExtension(editorPath)} at {editorPath}";
                        LogException(message, e);
                        Context.API.ShowMsgError(message);
                        return false;
                    }
                },
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
                        var message = $"Failed to open editor for file at {record.FullPath} with Shell {Path.GetFileNameWithoutExtension(shellPath)} at {shellPath}";
                        LogException(message, e);
                        Context.API.ShowMsgError(message);
                        return false;
                    }
                },
                IcoPath = Constants.FileImagePath
            };
        }

        private Result CreateAddToIndexSearchExclusionListResult(SearchResult record)
        {
            return new Result
            {
                Title = Context.API.GetTranslation("plugin_explorer_excludefromindexsearch"),
                SubTitle = Context.API.GetTranslation("plugin_explorer_path") + " " + record.FullPath,
                Action = _ =>
                {
                    if (!Settings.IndexSearchExcludedSubdirectoryPaths.Any(x => x.Path == record.FullPath))
                        Settings.IndexSearchExcludedSubdirectoryPaths.Add(new AccessLink
                        {
                            Path = record.FullPath
                        });

                    Task.Run(() =>
                    {
                        Context.API.ShowMsg(Context.API.GetTranslation("plugin_explorer_excludedfromindexsearch_msg"),
                            Context.API.GetTranslation("plugin_explorer_path") +
                            " " + record.FullPath, Constants.ExplorerIconImageFullPath);

                        // so the new path can be persisted to storage and not wait till next ViewModel save.
                        Context.API.SaveAppAllSettings();
                    });

                    return false;
                },
                IcoPath = Constants.ExcludeFromIndexImagePath
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
                IcoPath = Constants.IndexingOptionsIconImagePath
            };
        }

        public void LogException(string message, Exception e)
        {
            Log.Exception($"|Flow.Launcher.Plugin.Folder.ContextMenu|{message}", e);
        }

        private bool CanRunAsDifferentUser(string path)
        {
            switch (Path.GetExtension(path))
            {
                case ".exe":
                case ".bat":
                case ".msi":
                    return true;

                default:
                    return false;

            }
        }
    }
}
