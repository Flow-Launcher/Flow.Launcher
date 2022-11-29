using Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo;
using Flow.Launcher.Plugin.Explorer.Search.Everything;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using Flow.Launcher.Plugin.SharedCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Explorer.Exceptions;

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
                return x.Title == y.Title && x.SubTitle == y.SubTitle;
            }

            public int GetHashCode(Result obj)
            {
                return HashCode.Combine(obj.Title.GetHashCode(), obj.SubTitle?.GetHashCode() ?? 0);
            }
        }

        internal async Task<List<Result>> SearchAsync(Query query, CancellationToken token)
        {
            var results = new HashSet<Result>(PathEqualityComparator.Instance);

            // This allows the user to type the below action keywords and see/search the list of quick folder links
            if (ActionKeywordMatch(query, Settings.ActionKeyword.SearchActionKeyword)
                || ActionKeywordMatch(query, Settings.ActionKeyword.QuickAccessActionKeyword)
                || ActionKeywordMatch(query, Settings.ActionKeyword.PathSearchActionKeyword)
                || ActionKeywordMatch(query, Settings.ActionKeyword.IndexSearchActionKeyword)
                || ActionKeywordMatch(query, Settings.ActionKeyword.FileContentSearchActionKeyword))
            {
                if (string.IsNullOrEmpty(query.Search) && ActionKeywordMatch(query, Settings.ActionKeyword.QuickAccessActionKeyword))
                    return QuickAccess.AccessLinkListAll(query, Settings.QuickAccessLinks);

                var quickAccessLinks = QuickAccess.AccessLinkListMatched(query, Settings.QuickAccessLinks);

                results.UnionWith(quickAccessLinks);
            }
            else
            {
                return new List<Result>();
            }

            IAsyncEnumerable<SearchResult> searchResults = null;

            bool isPathSearch = query.Search.IsLocationPathString();

            switch (isPathSearch)
            {
                case true
                    when ActionKeywordMatch(query, Settings.ActionKeyword.PathSearchActionKeyword)
                          || ActionKeywordMatch(query, Settings.ActionKeyword.SearchActionKeyword):
                    
                    results.UnionWith(await PathSearchAsync(query, token).ConfigureAwait(false));
                    
                    return results.ToList();

                case false 
                    when ActionKeywordMatch(query, Settings.ActionKeyword.FileContentSearchActionKeyword):
                
                    // Intentionally require enabling of Everything's content search due to its slowness
                    if (Settings.ContentIndexProvider is EverythingSearchManager && !Settings.EnableEverythingContentSearch)
                        return EverythingContentSearchResult(query);
                    
                    searchResults = Settings.ContentIndexProvider.ContentSearchAsync("", query.Search, token);
                    
                    break;
                
                case false
                    when ActionKeywordMatch(query, Settings.ActionKeyword.IndexSearchActionKeyword)
                          || ActionKeywordMatch(query, Settings.ActionKeyword.SearchActionKeyword):
                    
                    searchResults = Settings.IndexProvider.SearchAsync(query.Search, token);
                    
                    break;
            }

            if (searchResults == null)
                return results.ToList();

            await foreach (var search in searchResults.WithCancellation(token).ConfigureAwait(false))
                results.Add(ResultManager.CreateResult(query, search));

            results.RemoveWhere(r => Settings.IndexSearchExcludedSubdirectoryPaths.Any(
                excludedPath => r.SubTitle.StartsWith(excludedPath.Path, StringComparison.OrdinalIgnoreCase)));

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
                                                                   keyword == Settings.QuickAccessActionKeyword,
                _ => throw new ArgumentOutOfRangeException(nameof(allowedActionKeyword), allowedActionKeyword, "actionKeyword out of range")
            };
        }

        private static List<Result> EverythingContentSearchResult(Query query)
        {
            return new List<Result>()
            {
                new()
                {
                    Title = "Do you want to enable content search for Everything?",
                    SubTitle = "It can be super slow without index (which is only supported in Everything 1.5+)",
                    IcoPath = "Images/search.png",
                    Action = c =>
                    {
                        Settings.EnableEverythingContentSearch = true;
                        Context.API.ChangeQuery(query.RawQuery, true);
                        return false;
                    }
                }
            };
        }

        private async Task<List<Result>> PathSearchAsync(Query query, CancellationToken token = default)
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

            var useIndexSearch = Settings.IndexSearchEngine is Settings.IndexSearchEngineOption.WindowsIndex
                                 && UseWindowsIndexForDirectorySearch(locationPath);

            var retrievedDirectoryPath = FilesFolders.ReturnPreviousDirectoryIfIncompleteString(locationPath);

            results.Add(retrievedDirectoryPath.EndsWith(":\\") 
                ? ResultManager.CreateDriveSpaceDisplayResult(retrievedDirectoryPath, useIndexSearch)
                : ResultManager.CreateOpenCurrentFolderResult(retrievedDirectoryPath, useIndexSearch));

            if (token.IsCancellationRequested)
                return new List<Result>();

            IEnumerable<SearchResult> directoryResult;

            var recursiveIndicatorIndex = query.Search.IndexOf('>');

            if (recursiveIndicatorIndex > 0 && Settings.PathEnumerationEngine != Settings.PathEnumerationEngineOption.DirectEnumeration)
            {
                directoryResult =
                    await Settings.PathEnumerator.EnumerateAsync(
                            query.Search[..recursiveIndicatorIndex],
                            query.Search[(recursiveIndicatorIndex + 1)..],
                            true,
                            token)
                        .ToListAsync(cancellationToken: token)
                        .ConfigureAwait(false);

            }
            else
            {
                try
                {
                    directoryResult = DirectoryInfoSearch.TopLevelDirectorySearch(query, query.Search, token);
                }
                catch (Exception e)
                {
                    throw new SearchException("DirectoryInfoSearch", e.Message, e);
                }
            }



            token.ThrowIfCancellationRequested();

            results.UnionWith(directoryResult.Select(searchResult => ResultManager.CreateResult(query, searchResult)));

            return results.ToList();
        }

        public static bool IsFileContentSearch(string actionKeyword) => actionKeyword == Settings.FileContentSearchActionKeyword;


        private bool UseWindowsIndexForDirectorySearch(string locationPath)
        {
            var pathToDirectory = FilesFolders.ReturnPreviousDirectoryIfIncompleteString(locationPath);

            return !Settings.IndexSearchExcludedSubdirectoryPaths.Any(
                x => FilesFolders.ReturnPreviousDirectoryIfIncompleteString(pathToDirectory).StartsWith(x.Path, StringComparison.OrdinalIgnoreCase)) 
                   && WindowsIndex.WindowsIndex.PathIsIndexed(pathToDirectory);
        }
    }
}
