using Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo;
using Flow.Launcher.Plugin.Explorer.Search.FolderLinks;
using Flow.Launcher.Plugin.Explorer.Search.WindowsIndex;
using Flow.Launcher.Plugin.SharedCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

        internal async Task<List<Result>> SearchAsync(Query query, CancellationToken token)
        {
            var results = new List<Result>();

            var querySearch = query.Search;

            if (IsFileContentSearch(query.ActionKeyword))
                return await WindowsIndexFileContentSearchAsync(query, querySearch, token).ConfigureAwait(false);

            // This allows the user to type the assigned action keyword and only see the list of quick folder links
            if (settings.QuickFolderAccessLinks.Count > 0
                && query.ActionKeyword == settings.SearchActionKeyword
                && string.IsNullOrEmpty(query.Search))
                return quickFolderAccess.FolderListAll(query, settings.QuickFolderAccessLinks, context);

            var quickFolderLinks = quickFolderAccess.FolderListMatched(query, settings.QuickFolderAccessLinks, context);

            if (quickFolderLinks.Count > 0)
                results.AddRange(quickFolderLinks);

            var isEnvironmentVariable = EnvironmentVariables.IsEnvironmentVariableSearch(querySearch);

            if (isEnvironmentVariable)
                return EnvironmentVariables.GetEnvironmentStringPathSuggestions(querySearch, query, context);

            // Query is a location path with a full environment variable, eg. %appdata%\somefolder\
            var isEnvironmentVariablePath = querySearch[1..].Contains("%\\");

            if (!querySearch.IsLocationPathString() && !isEnvironmentVariablePath)
            {
                results.AddRange(await WindowsIndexFilesAndFoldersSearchAsync(query, querySearch, token).ConfigureAwait(false));

                return results;
            }

            var locationPath = querySearch;

            if (isEnvironmentVariablePath)
                locationPath = EnvironmentVariables.TranslateEnvironmentVariablePath(locationPath);

            // Check that actual location exists, otherwise directory search will throw directory not found exception
            if (!FilesFolders.LocationExists(FilesFolders.ReturnPreviousDirectoryIfIncompleteString(locationPath)))
                return results;

            var useIndexSearch = UseWindowsIndexForDirectorySearch(locationPath);

            results.Add(resultManager.CreateOpenCurrentFolderResult(locationPath, useIndexSearch));

            if (token.IsCancellationRequested)
                return null;

            results.AddRange(await TopLevelDirectorySearchBehaviourAsync(WindowsIndexTopLevelFolderSearchAsync,
                                                                DirectoryInfoClassSearch,
                                                                useIndexSearch,
                                                                query,
                                                                locationPath,
                                                                token).ConfigureAwait(false));

            return results;
        }

        private async Task<List<Result>> WindowsIndexFileContentSearchAsync(Query query, string querySearchString, CancellationToken token)
        {
            var queryConstructor = new QueryConstructor(settings);

            if (string.IsNullOrEmpty(querySearchString))
                return new List<Result>();

            return await indexSearch.WindowsIndexSearchAsync(querySearchString,
                                                    queryConstructor.CreateQueryHelper().ConnectionString,
                                                    queryConstructor.QueryForFileContentSearch,
                                                    query,
                                                    token).ConfigureAwait(false);
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

        public async Task<List<Result>> TopLevelDirectorySearchBehaviourAsync(
            Func<Query, string, CancellationToken, Task<List<Result>>> windowsIndexSearch,
            Func<Query, string, List<Result>> directoryInfoClassSearch,
            bool useIndexSearch,
            Query query,
            string querySearchString,
            CancellationToken token)
        {
            if (!useIndexSearch)
                return directoryInfoClassSearch(query, querySearchString);

            return await windowsIndexSearch(query, querySearchString, token);
        }

        private async Task<List<Result>> WindowsIndexFilesAndFoldersSearchAsync(Query query, string querySearchString, CancellationToken token)
        {
            var queryConstructor = new QueryConstructor(settings);

            return await indexSearch.WindowsIndexSearchAsync(querySearchString,
                                                   queryConstructor.CreateQueryHelper().ConnectionString,
                                                   queryConstructor.QueryForAllFilesAndFolders,
                                                   query,
                                                   token).ConfigureAwait(false);
        }

        private async Task<List<Result>> WindowsIndexTopLevelFolderSearchAsync(Query query, string path, CancellationToken token)
        {
            var queryConstructor = new QueryConstructor(settings);

            return await indexSearch.WindowsIndexSearchAsync(path,
                                                   queryConstructor.CreateQueryHelper().ConnectionString,
                                                   queryConstructor.QueryForTopLevelDirectorySearch,
                                                   query,
                                                   token).ConfigureAwait(false);
        }

        private bool UseWindowsIndexForDirectorySearch(string locationPath)
        {
            var pathToDirectory = FilesFolders.ReturnPreviousDirectoryIfIncompleteString(locationPath);

            if (!settings.UseWindowsIndexForDirectorySearch)
                return false;

            if (settings.IndexSearchExcludedSubdirectoryPaths
                            .Any(x => FilesFolders.ReturnPreviousDirectoryIfIncompleteString(pathToDirectory)
                                        .StartsWith(x.Path, StringComparison.OrdinalIgnoreCase)))
                return false;

            return indexSearch.PathIsIndexed(pathToDirectory);
        }
    }
}
