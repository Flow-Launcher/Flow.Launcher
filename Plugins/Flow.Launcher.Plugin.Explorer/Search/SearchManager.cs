using Flow.Launcher.Plugin.Explorer.Search.WindowsIndex;
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

        public List<Result> Search(string querySearchString)
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

                return TopLevelFolderSearchBehaviour(WindowsIndexTopLevelFolderSearch,
                                                     DirectoryInfoClassSearch,
                                                     WindowsIndexExists,
                                                     querySearchString);
            }

            return WindowsIndexFilesAndFoldersSearch(querySearchString);
        }

        private List<Result> DirectoryInfoClassSearch(string arg)
        {
            throw new NotImplementedException();
        }

        ///<summary>
        }

        public List<Result> TopLevelFolderSearchBehaviour(
            Func<string, List<Result>> windowsIndexSearch,
            Func<string, List<Result>> directoryInfoClassSearch,
            Func<string, bool> indexExists,
            string path)
        {
            var results = windowsIndexSearch(path);

            if (results.Count == 0 && !indexExists(path))
                return directoryInfoClassSearch(path);

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
