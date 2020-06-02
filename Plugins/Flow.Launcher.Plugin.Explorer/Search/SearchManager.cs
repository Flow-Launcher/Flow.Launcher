using Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo;
using Flow.Launcher.Plugin.Explorer.Search.QuickFolderLinks;
using Flow.Launcher.Plugin.Explorer.Search.WindowsIndex;
using Flow.Launcher.Plugin.SharedCommands;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public class SearchManager
    {
        private Settings _settings;
        private PluginInitContext _context;

        private IndexSearch _indexSearch;

        private QuickFolderAccess quickFolderAccess = new QuickFolderAccess();

        public SearchManager(Settings settings, PluginInitContext context)
        {
            _settings = settings;
            _context = context;
            _indexSearch = new IndexSearch();
        }

        internal List<Result> Search(Query query)
        {
            var querySearch = query.Search;

            var quickFolderLinks = quickFolderAccess.FolderList(query, _settings.FolderLinks);

            if (quickFolderLinks.Count > 0)
                return quickFolderLinks;

            if (!FilesFolders.IsLocationPathString(querySearch))
                return WindowsIndexFilesAndFoldersSearch(query, querySearch);

            var locationPath = query.Search;

            if (EnvironmentVariables.IsEnvironmentVariableSearch(locationPath))
            {
                return EnvironmentVariables.GetEnvironmentStringPathSuggestions(locationPath, query);
            }

            // Query is a location path with a full environment variable, eg. %appdata%\somefolder\
            if (locationPath.Substring(1).Contains("%"))
            {
                locationPath = EnvironmentVariables.TranslateEnvironmentVariablePath(locationPath);
            }

            var results = new List<Result>();

            if (!FilesFolders.LocationExists(FilesFolders.GetPreviousLevelDirectoryIfPathIncomplete(locationPath)))
                return results;

            var useIndexSearch = UseWindowsIndexForDirectorySearch(locationPath);
            
            results.Add(ResultManager.CreateOpenCurrentFolderResult(locationPath, useIndexSearch));

            results.AddRange(TopLevelDirectorySearchBehaviour(WindowsIndexTopLevelFolderSearch,
                                                                DirectoryInfoClassSearch,
                                                                useIndexSearch,
                                                                query,
                                                                locationPath));

            return results;
        }

        private List<Result> DirectoryInfoClassSearch(Query query, string querySearch)
        {
            var directoryInfoSearch = new DirectoryInfoSearch(_settings);

            return directoryInfoSearch.TopLevelDirectorySearch(query, querySearch);
        }

        public List<Result> TopLevelDirectorySearchBehaviour(
            Func<Query, string, List<Result>> windowsIndexSearch,
            Func<Query, string, List<Result>> directoryInfoClassSearch,
            bool useIndexSearch,
            Query query,
            string querySearchString)
        {
            if (!useIndexSearch)
                return directoryInfoClassSearch(query, querySearchString);

            return windowsIndexSearch(query, querySearchString);
        }

        private List<Result> WindowsIndexFilesAndFoldersSearch(Query query, string querySearchString)
        {
            var queryConstructor = new QueryConstructor(_settings);

            return _indexSearch.WindowsIndexSearch(querySearchString,
                                                   queryConstructor.CreateQueryHelper().ConnectionString,
                                                   queryConstructor.QueryForAllFilesAndFolders,
                                                   query);
        }
        
        private List<Result> WindowsIndexTopLevelFolderSearch(Query query, string path)
        {
            var queryConstructor = new QueryConstructor(_settings);

            return _indexSearch.WindowsIndexSearch(path,
                                                   queryConstructor.CreateQueryHelper().ConnectionString,
                                                   queryConstructor.QueryForTopLevelDirectorySearch,
                                                   query);
        }

        private bool UseWindowsIndexForDirectorySearch(string locationPath)
        {
            if (!_settings.UseWindowsIndexForDirectorySearch)
                return false;

            if (_settings.WindowsIndexExcludedDirectories
                            .Any(x => x.Path == FilesFolders.GetPreviousLevelDirectoryIfPathIncomplete(locationPath)))
                return false;

            return _indexSearch.PathIsIndexed(locationPath);
        }
    }
}
