using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Plugin.Folder
{
    public class Main : IPlugin, ISettingProvider, IPluginI18n, ISavable, IContextMenu
    {
        public const string FolderImagePath = "Images\\folder.png";
        public const string FileImagePath = "Images\\file.png";
        public const string DeleteFileFolderImagePath = "Images\\deletefilefolder.png";
        public const string CopyImagePath = "Images\\copy.png";

        private string DefaultFolderSubtitleString = "Ctrl + Enter to open the directory";
        
        private static List<string> _driverNames;
        private PluginInitContext _context;

        private readonly Settings _settings;
        private readonly PluginJsonStorage<Settings> _storage;
        private IContextMenu _contextMenuLoader;

        private static Dictionary<string, string> _envStringPaths;

        public Main()
        {
            _storage = new PluginJsonStorage<Settings>();
            _settings = _storage.Load();
        }

        public void Save()
        {
            _storage.Save();
        }

        public Control CreateSettingPanel()
        {
            return new FileSystemSettings(_context.API, _settings);
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
            _contextMenuLoader = new ContextMenuLoader(context);
            InitialDriverList();
            LoadEnvironmentStringPaths();
        }

        public List<Result> Query(Query query)
        {
            var results = GetUserFolderResults(query);

            string search = query.Search.ToLower();
            if (!IsDriveOrSharedFolder(search) && !IsEnvironmentVariableSearch(search))
            {
                return results;
            }

            if (IsEnvironmentVariableSearch(search))
            {
                results.AddRange(GetEnvironmentStringPathResults(search, query));
            }
            else
            {
                results.AddRange(QueryInternal_Directory_Exists(query.Search, query));
            }

            // todo why was this hack here?
            foreach (var result in results)
            {
                result.Score += 10;
            }

            return results;
        }

        private static bool IsEnvironmentVariableSearch(string search)
        {
            return _envStringPaths != null && search.StartsWith("%");
        }

        private static bool IsDriveOrSharedFolder(string search)
        {
            if (search.StartsWith(@"\\"))
            { // shared folder
                return true;
            }

            if (_driverNames != null && _driverNames.Any(search.StartsWith))
            { // normal drive letter
                return true;
            }

            if (_driverNames == null && search.Length > 2 && char.IsLetter(search[0]) && search[1] == ':')
            { // when we don't have the drive letters we can try...
                return true; // we don't know so let's give it the possibility
            }

            return false;
        }

        private Result CreateFolderResult(string title, string subtitle, string path, Query query)
        {
            return new Result
            {
                Title = title,
                IcoPath = path,
                SubTitle = subtitle,
                TitleHighlightData = StringMatcher.FuzzySearch(query.Search, title).MatchData,
                Action = c =>
                {
                    if (c.SpecialKeyState.CtrlPressed)
                    {
                        try
                        {
                            FilesFolders.OpenPath(path);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "Could not start " + path);
                            return false;
                        }
                    }

                    string changeTo = path.EndsWith("\\") ? path : path + "\\";
                    _context.API.ChangeQuery(string.IsNullOrEmpty(query.ActionKeyword) ?
                        changeTo :
                        query.ActionKeyword + " " + changeTo);
                    return false;
                },
                ContextData = new SearchResult { Type = ResultType.Folder, FullPath = path }
            };
        }

        private List<Result> GetUserFolderResults(Query query)
        {
            string search = query.Search.ToLower();
            var userFolderLinks = _settings.FolderLinks.Where(
                x => x.Nickname.StartsWith(search, StringComparison.OrdinalIgnoreCase));
            var results = userFolderLinks.Select(item =>
                CreateFolderResult(item.Nickname, DefaultFolderSubtitleString, item.Path, query)).ToList();
            return results;
        }

        private void InitialDriverList()
        {
            if (_driverNames == null)
            {
                _driverNames = new List<string>();
                var allDrives = DriveInfo.GetDrives();
                foreach (DriveInfo driver in allDrives)
                {
                    _driverNames.Add(driver.Name.ToLower().TrimEnd('\\'));
                }
            }
        }

        private void LoadEnvironmentStringPaths()
        {
            _envStringPaths = new Dictionary<string, string>();

            foreach (DictionaryEntry special in Environment.GetEnvironmentVariables())
            {
                if (Directory.Exists(special.Value.ToString()))
                {
                    _envStringPaths.Add(special.Key.ToString().ToLower(), special.Value.ToString());
                }
            }
        }

        private static readonly char[] _specialSearchChars = new char[]
        {
            '?', '*', '>'
        };

        private List<Result> QueryInternal_Directory_Exists(string search, Query query)
        {
            var results = new List<Result>();
            var hasSpecial = search.IndexOfAny(_specialSearchChars) >= 0;
            string incompleteName = "";
            if (hasSpecial || !Directory.Exists(search + "\\"))
            {
                // if folder doesn't exist, we want to take the last part and use it afterwards to help the user 
                // find the right folder.
                int index = search.LastIndexOf('\\');
                if (index > 0 && index < (search.Length - 1))
                {
                    incompleteName = search.Substring(index + 1).ToLower();
                    search = search.Substring(0, index + 1);
                    if (!Directory.Exists(search))
                    {
                        return results;
                    }
                }
                else
                {
                    return results;
                }
            }
            else
            {
                // folder exist, add \ at the end of doesn't exist
                if (!search.EndsWith("\\"))
                {
                    search += "\\";
                }
            }

            results.Add(CreateOpenCurrentFolderResult(incompleteName, search));

            var searchOption = SearchOption.TopDirectoryOnly;
            incompleteName += "*";

            // give the ability to search all folder when starting with >
            if (incompleteName.StartsWith(">"))
            {
                searchOption = SearchOption.AllDirectories;

                // match everything before and after search term using supported wildcard '*', ie. *searchterm*
                incompleteName = "*" + incompleteName.Substring(1);
            }

            var folderList = new List<Result>();
            var fileList = new List<Result>();

            var folderSubtitleString = DefaultFolderSubtitleString;

            try
            {
                // search folder and add results
                var directoryInfo = new DirectoryInfo(search);
                var fileSystemInfos = directoryInfo.GetFileSystemInfos(incompleteName, searchOption);

                foreach (var fileSystemInfo in fileSystemInfos)
                {
                    if ((fileSystemInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;

                    if(fileSystemInfo is DirectoryInfo)
                    {
                        if (searchOption == SearchOption.AllDirectories)
                            folderSubtitleString = fileSystemInfo.FullName;

                        folderList.Add(CreateFolderResult(fileSystemInfo.Name, folderSubtitleString, fileSystemInfo.FullName, query));
                    }
                    else
                    {
                        fileList.Add(CreateFileResult(fileSystemInfo.FullName, query));
                    }
                }
            }
            catch (Exception e)
            {
                if (e is UnauthorizedAccessException || e is ArgumentException)
                {
                    results.Add(new Result { Title = e.Message, Score = 501 });

                    return results;
                }

                throw;
            }

            // Intial ordering, this order can be updated later by UpdateResultView.MainViewModel based on history of user selection.
            return results.Concat(folderList.OrderBy(x => x.Title)).Concat(fileList.OrderBy(x => x.Title)).ToList();
        }

        private List<Result> GetEnvironmentStringPathSuggestions(string search, Query query)
        {
            var results = new List<Result>();
            foreach (var p in _envStringPaths)
            {
                if (p.Key.StartsWith(search))
                {
                    results.Add(CreateFolderResult($"%{p.Key}%", p.Value, p.Value, query));
                }
            }
            return results;
        }

        private List<Result> GetEnvironmentStringPathResults(string envStringSearch, Query query)
        {
            if (envStringSearch == "%")
            { // return all environment string options as path suggestions
                return GetEnvironmentStringPathSuggestions("", query);
            }

            var results = new List<Result>();
            var search = envStringSearch.Substring(1);
            
            if (search.EndsWith("%") && search.Length > 1)
            { // query starts and ends with a %, find an exact match from env-string paths
                var exactEnvStringPath = search.Substring(0, search.Length-1);
                
                if (_envStringPaths.ContainsKey(exactEnvStringPath))
                {
                    var expandedPath = _envStringPaths[exactEnvStringPath];
                    results.Add(CreateFolderResult($"%{exactEnvStringPath}%", expandedPath, expandedPath, query));
                }
            }
            else if (search.Contains("%"))
            { // query starts with a % and contains another % somewhere before the end
                var splitSearch = search.Split("%");
                var exactEnvStringPath = splitSearch[0];

                // if there are more than 2 % characters in the query, don't bother
                if (splitSearch.Length == 2 && _envStringPaths.ContainsKey(exactEnvStringPath))
                {
                    var queryPartToReplace = $"%{exactEnvStringPath}%";
                    var expandedPath = _envStringPaths[exactEnvStringPath];
                    // replace the %envstring% part of the query with its expanded equivalent
                    var updatedSearch = envStringSearch.Replace(queryPartToReplace, expandedPath);

                    results.AddRange(QueryInternal_Directory_Exists(updatedSearch, query));
                }
            }
            else
            { // query simply starts wtih a %, suggest env-string paths that match the rest of the search
                results.AddRange(GetEnvironmentStringPathSuggestions(search, query));
            }

            return results;
        }

        private static Result CreateFileResult(string filePath, Query query)
        {
            var result = new Result
            {
                Title = Path.GetFileName(filePath),
                SubTitle = filePath,
                IcoPath = filePath,
                TitleHighlightData = StringMatcher.FuzzySearch(query.Search, Path.GetFileName(filePath)).MatchData,
                Action = c =>
                {
                    try
                    {
                        FilesFolders.OpenPath(filePath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Could not start " + filePath);
                    }

                    return true;
                },
                ContextData = new SearchResult { Type = ResultType.File, FullPath = filePath}
            };
            return result;
        }

        private static Result CreateOpenCurrentFolderResult(string incompleteName, string search)
        {
            var firstResult = "Open current directory";
            if (incompleteName.Length > 0)
                firstResult = "Open " + search;

            var folderName = search.TrimEnd('\\').Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.None).Last();

            return new Result
            {
                Title = firstResult,
                SubTitle = $"Use > to search files and subfolders within {folderName}, " +
                                $"* to search for file extensions in {folderName} or both >* to combine the search",
                IcoPath = search,
                Score = 500,
                Action = c =>
                {
                    FilesFolders.OpenPath(search);
                    return true;
                }
            };
        }

        public string GetTranslatedPluginTitle()
        {
            return _context.API.GetTranslation("flowlauncher_plugin_folder_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return _context.API.GetTranslation("flowlauncher_plugin_folder_plugin_description");
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            return _contextMenuLoader.LoadContextMenus(selectedResult);
        }
    }
}