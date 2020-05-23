using Flow.Launcher.Plugin.Explorer.Search.WindowsIndex;
using System;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public class SearchManager
    {
        private Settings _settings;
        private PluginInitContext _context;

        public SearchManager(Settings settings, PluginInitContext context)
        {
            _settings = settings;
            _context = context;
        }

        ///<summary>
        /// This checks whether a given string is a directory path or network location string. 
        /// It does not check if location actually exists.
        ///</summary>
        public bool IsLocationPathString(string querySearchString)
        {
            if (string.IsNullOrEmpty(querySearchString))
                return false;

            // // shared folder location, and not \\\location\
            if (querySearchString.Length >= 3
                && querySearchString.StartsWith(@"\\")
                && char.IsLetter(querySearchString[2]))
                return true;

            // c:\
            if (querySearchString.Length == 3
                && char.IsLetter(querySearchString[0]) 
                && querySearchString[1] == ':'
                && querySearchString[2] == '\\')
                return true;

            // c:\\
            if (querySearchString.Length >= 4
                && char.IsLetter(querySearchString[0])
                && querySearchString[1] == ':'
                && querySearchString[2] == '\\'
                && char.IsLetter(querySearchString[3]))
                return true;

            return false;
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

        internal List<Result> WindowsIndexFilesAndFoldersSearch(string querySearchString)
        {
            var queryConstructor = new QueryConstructor(_settings);

            var searcher = new IndexSearcher(_context);

            return searcher.WindowsIndexSearch(querySearchString,
                                               queryConstructor.CreateQueryHelper().ConnectionString,
                                               queryConstructor.QueryForAllFilesAndFolders);
        }
    }
}
