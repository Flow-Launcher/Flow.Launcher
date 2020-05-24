using Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo;
using Flow.Launcher.Plugin.Explorer.Search.WindowsIndex;
using Flow.Launcher.Plugin.SharedCommands;
using System;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public class SearchManager
    {
        private Settings _settings;
        private PluginInitContext _context;

        private IndexSearcher searcher;

        public SearchManager(Settings settings, PluginInitContext context)
        {
            _settings = settings;
            _context = context;
            searcher = new IndexSearcher(_context);
        }

        internal List<Result> Search(Query query)
        {
            var querySearch = query.Search;

            if (EnvironmentVariables.IsEnvironmentVariableSearch(querySearch)) 
            {
                return EnvironmentVariables.GetEnvironmentStringPathSuggestions(querySearch, query);
            }

            // Query is a location path with a full environment variable- starts with a % 
            // and contains another % somewhere before the end of the path
            if (querySearch.Substring(1).Contains("%"))
            {
                querySearch = EnvironmentVariables.TranslateEnvironmentVariablePath(querySearch);
            }

            if (FilesFolders.IsLocationPathString(querySearch))
            {
                return TopLevelFolderSearchBehaviour(WindowsIndexTopLevelFolderSearch,
                                                     DirectoryInfoClassSearch,
                                                     WindowsIndexExists,
                                                     query,
                                                     querySearch);
            }

            return WindowsIndexFilesAndFoldersSearch(querySearch);
        }

        private List<Result> DirectoryInfoClassSearch(Query query, string querySearch)
        {
            var directoryInfoSearch = new DirectoryInfoSearch(_settings, _context);

            return directoryInfoSearch.TopLevelDirectorySearch(query, querySearch);
        }

        public List<Result> TopLevelFolderSearchBehaviour(
            Func<string, List<Result>> windowsIndexSearch,
            Func<Query, string, List<Result>> directoryInfoClassSearch,
            Func<string, bool> indexExists,
            Query query,
            string querySearchString)
        {
            var results = windowsIndexSearch(querySearchString);

            if (results.Count == 0 && !indexExists(querySearchString))
                return directoryInfoClassSearch(query, querySearchString);

            return results;
        }

        private List<Result> WindowsIndexFilesAndFoldersSearch(string querySearchString)
        {
            var queryConstructor = new QueryConstructor(_settings);

            return searcher.WindowsIndexSearch(querySearchString,
                                               queryConstructor.CreateQueryHelper().ConnectionString,
                                               queryConstructor.QueryForAllFilesAndFolders);
        }
        
        private List<Result> WindowsIndexTopLevelFolderSearch(string path)
        {
            var queryConstructor = new QueryConstructor(_settings);

            return searcher.WindowsIndexSearch(path,
                                               queryConstructor.CreateQueryHelper().ConnectionString,
                                               queryConstructor.QueryForTopLevelDirectorySearch);
        }

        private bool WindowsIndexExists(string path)
        {
            return searcher.PathIsIndexed(path);
        }
    }
}
