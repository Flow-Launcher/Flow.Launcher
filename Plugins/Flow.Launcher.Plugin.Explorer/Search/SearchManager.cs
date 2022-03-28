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
        internal static PluginInitContext Context;

        internal static Settings Settings;

        public SearchManager(Settings settings, PluginInitContext context)
        {
            Context = context;
            Settings = settings;
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

            var results = new HashSet<Result>(PathEqualityComparator.Instance);

            // This allows the user to type the below action keywords and see/search the list of quick folder links
            if (ActionKeywordMatch(query, Settings.ActionKeyword.SearchActionKeyword)
                || ActionKeywordMatch(query, Settings.ActionKeyword.QuickAccessActionKeyword)
                || ActionKeywordMatch(query, Settings.ActionKeyword.PathSearchActionKeyword))
            {
                if (string.IsNullOrEmpty(query.Search))
                    return QuickAccess.AccessLinkListAll(query, Settings.QuickAccessLinks);

                var quickaccessLinks = QuickAccess.AccessLinkListMatched(query, Settings.QuickAccessLinks);

                results.UnionWith(quickaccessLinks);
            }

            IEnumerable<SearchResult> searchResults;

            if (IsFileContentSearch(query.ActionKeyword))
            {
                searchResults = await Settings.ContentIndexProvider.ContentSearchAsync(query.Search, token);
            }
            else
            {
                searchResults = await Settings.IndexProvider.SearchAsync(query.Search, token);
            }

            if (ActionKeywordMatch(query, Settings.ActionKeyword.PathSearchActionKeyword) ||
                ActionKeywordMatch(query, Settings.ActionKeyword.SearchActionKeyword))
            {
                results.UnionWith(await PathSearchAsync(query, token).ConfigureAwait(false));
            }

            if ((ActionKeywordMatch(query, Settings.ActionKeyword.IndexSearchActionKeyword) ||
                 ActionKeywordMatch(query, Settings.ActionKeyword.SearchActionKeyword)) &&
                querySearch.Length > 0 &&
                !querySearch.IsLocationPathString())
            {
                if (searchResults != null)
                    results.UnionWith(searchResults.Select(search => ResultManager.CreateResult(query, search)));
            }

            return results.ToList();
        }

        private bool ActionKeywordMatch(Query query, Settings.ActionKeyword allowedActionKeyword)
        {
            var keyword = query.ActionKeyword.Length == 0 ? Query.GlobalPluginWildcardSign : query.ActionKeyword;

            return allowedActionKeyword switch
            {
                Settings.ActionKeyword.SearchActionKeyword => Settings.SearchActionKeywordEnabled &&
                                                              keyword == Settings.SearchActionKeyword,
                Settings.ActionKeyword.PathSearchActionKeyword => Settings.PathSearchKeywordEnabled &&
                                                                  keyword == Settings.PathSearchActionKeyword,
                Settings.ActionKeyword.FileContentSearchActionKeyword => Settings.FileContentSearchKeywordEnabled &&
                                                                         keyword == Settings.FileContentSearchActionKeyword,
                Settings.ActionKeyword.IndexSearchActionKeyword => Settings.IndexSearchKeywordEnabled &&
                                                                   keyword == Settings.IndexSearchActionKeyword,
                Settings.ActionKeyword.QuickAccessActionKeyword => Settings.QuickAccessKeywordEnabled &&
                                                                   keyword == Settings.QuickAccessActionKeyword
            };
        }

        public async Task<List<Result>> PathSearchAsync(Query query, CancellationToken token = default)
        {
            var querySearch = query.Search;

            var results = new HashSet<Result>(PathEqualityComparator.Instance);

            var isEnvironmentVariable = EnvironmentVariables.IsEnvironmentVariableSearch(querySearch);

            if (isEnvironmentVariable)
                return EnvironmentVariables.GetEnvironmentStringPathSuggestions(querySearch, query, Context);

            // Query is a location path with a full environment variable, eg. %appdata%\somefolder\
            var isEnvironmentVariablePath = querySearch[1..].Contains("%\\");

            var locationPath = querySearch;
            
            if (isEnvironmentVariablePath)
                locationPath = EnvironmentVariables.TranslateEnvironmentVariablePath(locationPath);

            // Check that actual location exists, otherwise directory search will throw directory not found exception
            if (!FilesFolders.ReturnPreviousDirectoryIfIncompleteString(locationPath).LocationExists())
                return results.ToList();

            var useIndexSearch = UseWindowsIndexForDirectorySearch(locationPath);

            results.Add(ResultManager.CreateOpenCurrentFolderResult(locationPath, useIndexSearch));

            token.ThrowIfCancellationRequested();

            IEnumerable<SearchResult> directoryResult;

            if (query.Search.Contains('>'))
            {
                directoryResult =
                    await Settings.PathEnumerator.EnumerateAsync(locationPath, "", false, token)
                        .ConfigureAwait(false);
            }
            else
            {
                directoryResult = DirectoryInfoSearch.TopLevelDirectorySearch(query, query.Search, token);
            }
            
            

            token.ThrowIfCancellationRequested();

            results.UnionWith(directoryResult.Select(searchResult => ResultManager.CreateResult(query, searchResult)));

            return results.ToList();
        }

        public bool IsFileContentSearch(string actionKeyword)
        {
            return actionKeyword == Settings.FileContentSearchActionKeyword;
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

        private bool UseWindowsIndexForDirectorySearch(string locationPath)
        {
            var pathToDirectory = FilesFolders.ReturnPreviousDirectoryIfIncompleteString(locationPath);

            if (!Settings.UseWindowsIndexForDirectorySearch)
                return false;

            if (Settings.IndexSearchExcludedSubdirectoryPaths
                .Any(x => FilesFolders.ReturnPreviousDirectoryIfIncompleteString(pathToDirectory)
                    .StartsWith(x.Path, StringComparison.OrdinalIgnoreCase)))
                return false;

            return WindowsIndex.WindowsIndex.PathIsIndexed(pathToDirectory);
        }
    }
}