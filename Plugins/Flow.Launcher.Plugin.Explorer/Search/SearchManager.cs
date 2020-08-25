using Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo;
using Flow.Launcher.Plugin.Explorer.Search.FolderLinks;
using Flow.Launcher.Plugin.Explorer.Search.WindowsIndex;
using Flow.Launcher.Plugin.SharedCommands;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public class SearchManager
    {
        private readonly PluginInitContext context;

        private readonly IndexSearch indexSearch;

        private readonly QuickFolderAccess quickFolderAccess = new QuickFolderAccess();

        private readonly ResultManager resultManager;

        private readonly Settings settings;

        public SearchManager(Settings settings, PluginInitContext context)
        {
            this.context = context;
            indexSearch = new IndexSearch(context);
            resultManager = new ResultManager(context);
            this.settings = settings;
        }

        internal List<Result> Search(Query query)
        {
            var results = new List<Result>();

            var querySearch = query.Search;

            var quickFolderLinks = quickFolderAccess.FolderList(query, settings.QuickFolderAccessLinks, context);

            var quickFolderLinks = quickFolderAccess.FolderListMatched(query, settings.QuickFolderAccessLinks, context);

            if (string.IsNullOrEmpty(querySearch))
                return results;

            if (IsFileContentSearch(query.ActionKeyword))
                return WindowsIndexFileContentSearch(query, querySearch);

            var isEnvironmentVariable = EnvironmentVariables.IsEnvironmentVariableSearch(querySearch);

            if (isEnvironmentVariable)
                return EnvironmentVariables.GetEnvironmentStringPathSuggestions(querySearch, query, context);

            // Query is a location path with a full environment variable, eg. %appdata%\somefolder\
            var isEnvironmentVariablePath = querySearch.Substring(1).Contains("%\\");

            if (!FilesFolders.IsLocationPathString(querySearch) && !isEnvironmentVariablePath)
                return WindowsIndexFilesAndFoldersSearch(query, querySearch);

            var locationPath = querySearch;

            if (isEnvironmentVariablePath)
                locationPath = EnvironmentVariables.TranslateEnvironmentVariablePath(locationPath);

            if (!FilesFolders.LocationExists(FilesFolders.ReturnPreviousDirectoryIfIncompleteString(locationPath)))
                return results;

            var useIndexSearch = UseWindowsIndexForDirectorySearch(locationPath);
            
            results.Add(resultManager.CreateOpenCurrentFolderResult(locationPath, useIndexSearch));

            results.AddRange(TopLevelDirectorySearchBehaviour(WindowsIndexTopLevelFolderSearch,
                                                                DirectoryInfoClassSearch,
                                                                useIndexSearch,
                                                                query,
                                                                locationPath));

            return results;
        }

        private List<Result> WindowsIndexFileContentSearch(Query query, string querySearchString)
        {
            var queryConstructor = new QueryConstructor(settings);

            if (string.IsNullOrEmpty(querySearchString))
                return new List<Result>();

            return indexSearch.WindowsIndexSearch(querySearchString,
                                                    queryConstructor.CreateQueryHelper().ConnectionString,
                                                    queryConstructor.QueryForFileContentSearch,
                                                    query);
        }

        public bool IsFileContentSearch(string actionKeyword)
        {
            return actionKeyword == settings.FileContentSearchActionKeyword;
        }

        private List<Result> DirectoryInfoClassSearch(Query query, string querySearch)
        {
            var directoryInfoSearch = new DirectoryInfoSearch(context);

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
            var queryConstructor = new QueryConstructor(settings);

            return indexSearch.WindowsIndexSearch(querySearchString,
                                                   queryConstructor.CreateQueryHelper().ConnectionString,
                                                   queryConstructor.QueryForAllFilesAndFolders,
                                                   query);
        }
        
        private List<Result> WindowsIndexTopLevelFolderSearch(Query query, string path)
        {
            var queryConstructor = new QueryConstructor(settings);

            return indexSearch.WindowsIndexSearch(path,
                                                   queryConstructor.CreateQueryHelper().ConnectionString,
                                                   queryConstructor.QueryForTopLevelDirectorySearch,
                                                   query);
        }

        private bool UseWindowsIndexForDirectorySearch(string locationPath)
        {
            if (!settings.UseWindowsIndexForDirectorySearch)
                return false;

            if (settings.IndexSearchExcludedSubdirectoryPaths
                            .Any(x => FilesFolders.ReturnPreviousDirectoryIfIncompleteString(locationPath)
                                        .StartsWith(x.Path, StringComparison.OrdinalIgnoreCase)))
                return false;

            return indexSearch.PathIsIndexed(locationPath);
        }
    }
}
