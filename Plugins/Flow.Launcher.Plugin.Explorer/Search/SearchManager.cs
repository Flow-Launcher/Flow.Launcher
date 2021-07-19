using Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
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

        private readonly Settings settings;

        public SearchManager(Settings settings, PluginInitContext context)
        {
            this.context = context;
            this.settings = settings;
        }

        private class PathEqualityComparator : IEqualityComparer<Result>
        {
            private static PathEqualityComparator instance;
            public static PathEqualityComparator Instance => instance ??= new PathEqualityComparator();

            public bool Equals(Result x, Result y)
            {
                return x.SubTitle == y.SubTitle;
            }

            public int GetHashCode(Result obj)
            {
                return obj.SubTitle.GetHashCode();
            }
        }

        internal async Task<List<Result>> SearchAsync(Query query, CancellationToken token)
        {
            var querySearch = query.Search;

            if (IsFileContentSearch(query.ActionKeyword))
                return await WindowsIndexFileContentSearchAsync(query, querySearch, token).ConfigureAwait(false);

            var result = new HashSet<Result>(PathEqualityComparator.Instance);

            if (ActionKeywordMatch(query, Settings.ActionKeyword.PathSearchActionKeyword) ||
                ActionKeywordMatch(query, Settings.ActionKeyword.SearchActionKeyword))
            {
                result.UnionWith(await PathSearchAsync(query, token).ConfigureAwait(false));
            }

            if ((ActionKeywordMatch(query, Settings.ActionKeyword.IndexSearchActionKeyword) ||
                 ActionKeywordMatch(query, Settings.ActionKeyword.SearchActionKeyword)) &&
                querySearch.Length > 0 &&
                !querySearch.IsLocationPathString())
            {
                result.UnionWith(await WindowsIndexFilesAndFoldersSearchAsync(query, querySearch, token)
                    .ConfigureAwait(false));
            }

            return result.ToList();
        }

        private bool ActionKeywordMatch(Query query, Settings.ActionKeyword allowedActionKeyword)
        {
            var keyword = query.ActionKeyword.Length == 0 ? Query.GlobalPluginWildcardSign : query.ActionKeyword;

            return allowedActionKeyword switch
            {
                Settings.ActionKeyword.SearchActionKeyword => settings.SearchActionKeywordEnabled &&
                                                              keyword == settings.SearchActionKeyword,
                Settings.ActionKeyword.PathSearchActionKeyword => settings.PathSearchKeywordEnabled &&
                                                                  keyword == settings.PathSearchActionKeyword,
                Settings.ActionKeyword.FileContentSearchActionKeyword => keyword ==
                                                                         settings.FileContentSearchActionKeyword,
                Settings.ActionKeyword.IndexSearchActionKeyword => settings.IndexOnlySearchKeywordEnabled &&
                                                                       keyword == settings.IndexSearchActionKeyword
            };
        }

        public async Task<List<Result>> PathSearchAsync(Query query, CancellationToken token = default)
        {
            var querySearch = query.Search;

            // This allows the user to type the assigned action keyword and only see the list of quick folder links
            if (string.IsNullOrEmpty(query.Search))
                return QuickAccess.AccessLinkListAll(query, settings.QuickAccessLinks);

            var results = new HashSet<Result>(PathEqualityComparator.Instance);

            var quickaccessLinks = QuickAccess.AccessLinkListMatched(query, settings.QuickAccessLinks);

            results.UnionWith(quickaccessLinks);

            var isEnvironmentVariable = EnvironmentVariables.IsEnvironmentVariableSearch(querySearch);

            if (isEnvironmentVariable)
                return EnvironmentVariables.GetEnvironmentStringPathSuggestions(querySearch, query, context);

            // Query is a location path with a full environment variable, eg. %appdata%\somefolder\
            var isEnvironmentVariablePath = querySearch[1..].Contains("%\\");

            var locationPath = querySearch;

            if (isEnvironmentVariablePath)
                locationPath = EnvironmentVariables.TranslateEnvironmentVariablePath(locationPath);

            // Check that actual location exists, otherwise directory search will throw directory not found exception
            if (!FilesFolders.LocationExists(FilesFolders.ReturnPreviousDirectoryIfIncompleteString(locationPath)))
                return results.ToList();

            var useIndexSearch = UseWindowsIndexForDirectorySearch(locationPath);

            results.Add(ResultManager.CreateOpenCurrentFolderResult(locationPath, useIndexSearch));

            token.ThrowIfCancellationRequested();

            var directoryResult = await TopLevelDirectorySearchBehaviourAsync(WindowsIndexTopLevelFolderSearchAsync,
                DirectoryInfoClassSearch,
                useIndexSearch,
                query,
                locationPath,
                token).ConfigureAwait(false);

            token.ThrowIfCancellationRequested();

            results.UnionWith(directoryResult);

            return results.ToList();
        }

        private async Task<List<Result>> WindowsIndexFileContentSearchAsync(Query query, string querySearchString,
            CancellationToken token)
        {
            var queryConstructor = new QueryConstructor(settings);

            if (string.IsNullOrEmpty(querySearchString))
                return new List<Result>();

            return await IndexSearch.WindowsIndexSearchAsync(querySearchString,
                queryConstructor.CreateQueryHelper().ConnectionString,
                queryConstructor.QueryForFileContentSearch,
                settings.IndexSearchExcludedSubdirectoryPaths,
                query,
                token).ConfigureAwait(false);
        }

        public bool IsFileContentSearch(string actionKeyword)
        {
            return actionKeyword == settings.FileContentSearchActionKeyword;
        }

        private List<Result> DirectoryInfoClassSearch(Query query, string querySearch, CancellationToken token)
        {
            return DirectoryInfoSearch.TopLevelDirectorySearch(query, querySearch, token);
        }

        public async Task<List<Result>> TopLevelDirectorySearchBehaviourAsync(
            Func<Query, string, CancellationToken, Task<List<Result>>> windowsIndexSearch,
            Func<Query, string, CancellationToken, List<Result>> directoryInfoClassSearch,
            bool useIndexSearch,
            Query query,
            string querySearchString,
            CancellationToken token)
        {
            if (!useIndexSearch)
                return directoryInfoClassSearch(query, querySearchString, token);

            return await windowsIndexSearch(query, querySearchString, token);
        }

        private async Task<List<Result>> WindowsIndexFilesAndFoldersSearchAsync(Query query, string querySearchString,
            CancellationToken token)
        {
            var queryConstructor = new QueryConstructor(settings);

            return await IndexSearch.WindowsIndexSearchAsync(querySearchString,
                queryConstructor.CreateQueryHelper().ConnectionString,
                queryConstructor.QueryForAllFilesAndFolders,
                settings.IndexSearchExcludedSubdirectoryPaths,
                query,
                token).ConfigureAwait(false);
        }

        private async Task<List<Result>> WindowsIndexTopLevelFolderSearchAsync(Query query, string path,
            CancellationToken token)
        {
            var queryConstructor = new QueryConstructor(settings);

            return await IndexSearch.WindowsIndexSearchAsync(path,
                queryConstructor.CreateQueryHelper().ConnectionString,
                queryConstructor.QueryForTopLevelDirectorySearch,
                settings.IndexSearchExcludedSubdirectoryPaths,
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

            return IndexSearch.PathIsIndexed(pathToDirectory);
        }
    }
}