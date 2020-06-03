using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Plugin.Explorer.Search;
using System.Linq;
using System.Reflection;

namespace Flow.Launcher.Plugin.Explorer
{
    internal class ContextMenu : IContextMenu
    {
        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var contextMenus = new List<Result>();
            if (selectedResult.ContextData is SearchResult record)
            {
                if (record.Type == ResultType.File)
                    contextMenus.Add(CreateOpenWithEditorResult(record));

                if (record.Type == ResultType.Folder && record.WindowsIndexed)
                    contextMenus.Add(CreateAddToIndexSearchExclusionListResult(record));

                contextMenus.Add(CreateOpenContainingFolderResult(record));

                if (record.ShowIndexState)
                    contextMenus.Add(new Result {Title = "From index search: " + (record.WindowsIndexed ? "Yes" : "No"), 
                                                    SubTitle = "Location: " + record.FullPath,
                                                    Score = 501, IcoPath = Constants.IndexImagePath});

                var icoPath = (record.Type == ResultType.File) ? Constants.FileImagePath : Constants.FolderImagePath;
                var fileOrFolder = (record.Type == ResultType.File) ? "file" : "folder";
                contextMenus.Add(new Result
                {
                    Title = "Copy path",
                    SubTitle = $"Copy the current {fileOrFolder} path to clipboard",
                    Action = (context) =>
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
                            Main.Context.API.ShowMsg(message);
                            return false;
                        }
                    },
                    IcoPath = Constants.CopyImagePath
                });

                contextMenus.Add(new Result
                {
                    Title = $"Copy {fileOrFolder}",
                    SubTitle = $"Copy the {fileOrFolder} to clipboard",
                    Action = (context) =>
                    {
                        try
                        {
                            Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection { record.FullPath });
                            return true;
                        }
                        catch (Exception e)
                        {
                            var message = $"Fail to set {fileOrFolder} in clipboard";
                            LogException(message, e);
                            Main.Context.API.ShowMsg(message);
                            return false;
                        }

                    },
                    IcoPath = icoPath
                });

                if (record.Type == ResultType.File || record.Type == ResultType.Folder)
                    contextMenus.Add(new Result
                    {
                        Title = $"Delete {fileOrFolder}",
                        SubTitle = $"Delete the selected {fileOrFolder}",
                        Action = (context) =>
                        {
                            try
                            {
                                if (record.Type == ResultType.File)
                                    File.Delete(record.FullPath);
                                else
                                    Directory.Delete(record.FullPath, true);
                            }
                            catch (Exception e)
                            {
                                var message = $"Fail to delete {fileOrFolder} at {record.FullPath}";
                                LogException(message, e);
                                Main.Context.API.ShowMsg(message);
                                return false;
                            }

                            return true;
                        },
                        IcoPath = Constants.DeleteFileFolderImagePath
                    });

                if (record.Type == ResultType.File && CanRunAsDifferentUser(record.FullPath))
                    contextMenus.Add(new Result
                    {
                        Title = "Run as different user",
                        Action = (context) =>
                        {
                            try
                            {
                                Task.Run(() => ShellCommand.RunAsDifferentUser(record.FullPath.SetProcessStartInfo()));
                            }
                            catch (FileNotFoundException e)
                            {
                                var name = "Plugin: Folder";
                                var message = $"File not found: {e.Message}";
                                Main.Context.API.ShowMsg(name, message);
                            }

                            return true;
                        },
                        IcoPath = "Images/user.png"
                    });
            }

            return contextMenus;
        }

        private Result CreateOpenContainingFolderResult(SearchResult record)
        {
            return new Result
            {
                Title = "Open containing folder",
                Action = _ =>
                {
                    try
                    {
                        Process.Start("explorer.exe", $" /select,\"{record.FullPath}\"");
                    }
                    catch (Exception e)
                    {
                        var message = $"Fail to open file at {record.FullPath}";
                        LogException(message, e);
                        Main.Context.API.ShowMsg(message);
                        return false;
                    }

                    return true;
                },
                IcoPath = Constants.FolderImagePath
            };
        }

        private Result CreateOpenWithEditorResult(SearchResult record)
        {
            string editorPath = "notepad.exe"; // TODO add the ability to create a custom editor

            var name = "Open With Editor: " + Path.GetFileNameWithoutExtension(editorPath);
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
                        var message = $"Fail to editor for file at {record.FullPath}";
                        LogException(message, e);
                        Main.Context.API.ShowMsg(message);
                        return false;
                    }
                },
                IcoPath = editorPath
            };
        }

        private Result CreateAddToIndexSearchExclusionListResult(SearchResult record)
        {
            return new Result
            {
                Title = "Exclude current and sub-directories from index search",
                SubTitle = "Path: " + record.FullPath,
                Action = _ =>
                {
                    if(!Main.Settings.IndexSearchExcludedSubdirectoryPaths.Any(x => x == record.FullPath))
                        Main.Settings.IndexSearchExcludedSubdirectoryPaths.Add(record.FullPath);

                    var pluginDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location.ToString());

                    var iconPath = pluginDirectory + "\\" + Constants.ExplorerIconImagePath;

                    Task.Run(() =>
                    {
                        Main.Context.API.ShowMsg("Excluded from Index Search", "Path: " + record.FullPath, iconPath);

                        // so the new path can be persisted to storage and not wait till next ViewModel save.
                        Main.Context.API.SaveAppAllSettings();
                    });

                    return false;
                },
                IcoPath = Constants.ExcludeFromIndexImagePath
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
