using Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using Flow.Launcher.Plugin.SharedCommands;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Explorer.Exceptions;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public class SearchManager
    {
        internal PluginInitContext Context;

        internal Settings Settings;

        public SearchManager(Settings settings, PluginInitContext context)
        {
            Context = context;
            Settings = settings;
        }

        /// <summary>
        /// Note: A path that ends with "\" and one that doesn't will not be regarded as equal.
        /// </summary>
        public class PathEqualityComparator : IEqualityComparer<SearchResult>
        {
            private static PathEqualityComparator instance;

            public static PathEqualityComparator Instance => instance ??= new PathEqualityComparator();

            public bool Equals(SearchResult x, SearchResult y)
            {
                return x.FullPath.Equals(y.FullPath, StringComparison.InvariantCultureIgnoreCase);
            }

            public int GetHashCode(SearchResult obj)
            {
                return StringComparer.InvariantCultureIgnoreCase.GetHashCode(obj.FullPath);
            }
        }

        internal async Task<List<SearchResult>> SearchAsync(Query query, CancellationToken token)
        {
            var results = new HashSet<SearchResult>(PathEqualityComparator.Instance);

            var task = GetQueryTask(query);

            // This allows the user to type the below action keywords and see/search the list of quick folder links
            if (task.HasFlag(SearchTask.QuickAccessSearch))
            {
                if (string.IsNullOrEmpty(query.Search))
                {
                    results.UnionWith(QuickAccess.AccessLinkListAll(query, Settings.QuickAccessLinks));
                }
                else
                {
                    var quickAccessLinks = QuickAccess.AccessLinkListMatched(query, Settings.QuickAccessLinks);
                    results.UnionWith(quickAccessLinks);
                }
            }

            IAsyncEnumerable<SearchResult> searchResults = null;

            bool isPathSearch = query.Search.IsLocationPathString() || IsEnvironmentVariableSearch(query.Search);

            string engineName = "";

            if (task.HasFlag(SearchTask.PathSearch) && isPathSearch)
            {
                await foreach (var path in PathSearchAsync(query, token).ConfigureAwait(false))
                {
                    results.Add(path);
                }

                return results.ToList();
            }

            if (task.HasFlag(SearchTask.IndexSearch))
            {
                searchResults = Settings.IndexProvider.SearchAsync(query.Search, token);
                engineName = Enum.GetName(Settings.IndexSearchEngine);
            }

            if (task.HasFlag(SearchTask.FileContentSearch))
            {
                if (!Settings.EnableEverythingContentSearch && Settings.ContentSearchEngine == Settings.ContentIndexSearchEngineOption.Everything)
                    ThrowEverythingContentSearchUnavailable(query);

                searchResults = Settings.ContentIndexProvider.ContentSearchAsync("", query.Search, token);
                engineName = Enum.GetName(Settings.ContentSearchEngine);
            }

            try
            {
                if (searchResults != null)
                {
                    await foreach (var result in searchResults.WithCancellation(token))
                    {
                        results.Add(result);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (EngineNotAvailableException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new SearchException(engineName, e.Message, e);
            }

            results.RemoveWhere(r => Settings.IndexSearchExcludedSubdirectoryPaths.Any(
                excludedPath => excludedPath.Path.PathContains(r.FullPath)));

            return results.ToList();
        }

        private SearchTask GetQueryTask(Query query)
        {
            SearchTask task = SearchTask.None;

            if (ActionKeywordMatch(query, Settings.ActionKeyword.SearchActionKeyword))
                task |= SearchTask.IndexSearch | SearchTask.PathSearch | SearchTask.QuickAccessSearch;

            if (ActionKeywordMatch(query, Settings.ActionKeyword.QuickAccessActionKeyword))
                task |= SearchTask.QuickAccessSearch;

            if (ActionKeywordMatch(query, Settings.ActionKeyword.PathSearchActionKeyword))
                task |= SearchTask.PathSearch;

            if (ActionKeywordMatch(query, Settings.ActionKeyword.IndexSearchActionKeyword))
                task |= SearchTask.IndexSearch;

            if (ActionKeywordMatch(query, Settings.ActionKeyword.FileContentSearchActionKeyword))
                task |= SearchTask.FileContentSearch;

            return task;
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

        [DoesNotReturn]
        private void ThrowEverythingContentSearchUnavailable(Query query)
        {
            throw new EngineNotAvailableException(nameof(Settings.ContentIndexSearchEngineOption.Everything),
                Context.API.GetTranslation("flowlauncher_plugin_everything_enable_content_search_tips"),
                Context.API.GetTranslation("flowlauncher_plugin_everything_enable_content_search"),
                _ =>
                {
                    Settings.EnableEverythingContentSearch = true;
                    Context.API.ChangeQuery(query.RawQuery, true);

                    return ValueTask.FromResult(false);
                });

        }

        private async IAsyncEnumerable<SearchResult> PathSearchAsync(Query query, CancellationToken token = default)
        {
            var querySearch = query.Search;

            if (EnvironmentVariables.IsEnvironmentVariableSearch(querySearch))
            {
                foreach (var envResult in EnvironmentVariables.GetEnvironmentStringPathSuggestions(querySearch, query, Context))
                {
                    yield return envResult;
                }

                yield break;
            }

            // Query is a location path with a full environment variable, eg. %appdata%\somefolder\, c:\users\%USERNAME%\downloads
            var needToExpand = EnvironmentVariables.HasEnvironmentVar(querySearch);
            var locationPath = needToExpand ? Environment.ExpandEnvironmentVariables(querySearch) : querySearch;

            // Check that actual location exists, otherwise directory search will throw directory not found exception
            if (!FilesFolders.ReturnPreviousDirectoryIfIncompleteString(locationPath).LocationExists())
                yield break;

            var useIndexSearch = Settings.IndexSearchEngine is Settings.IndexSearchEngineOption.WindowsIndex
                                 && UseWindowsIndexForDirectorySearch(locationPath);

            var retrievedDirectoryPath = FilesFolders.ReturnPreviousDirectoryIfIncompleteString(locationPath);

            yield return new SearchResult()
            {
                FullPath = retrievedDirectoryPath,
                Type = retrievedDirectoryPath.EndsWith(":\\")
                    ? ResultType.Volume
                    : ResultType.CurrentFolder,
                Score = 100,
                WindowsIndexed = useIndexSearch
            };

            if (token.IsCancellationRequested)
                yield break;

            IAsyncEnumerable<SearchResult> directoryResult;

            var recursiveIndicatorIndex = query.Search.IndexOf('>');

            if (recursiveIndicatorIndex > 0 && Settings.PathEnumerationEngine != Settings.PathEnumerationEngineOption.DirectEnumeration)
            {
                directoryResult =
                    Settings.PathEnumerator.EnumerateAsync(
                        query.Search[..recursiveIndicatorIndex],
                        query.Search[(recursiveIndicatorIndex + 1)..],
                        true,
                        token);
            }
            else
            {
                directoryResult = DirectoryInfoSearch.TopLevelDirectorySearch(query, query.Search, token).ToAsyncEnumerable();
            }

            if (token.IsCancellationRequested)
                yield break;

            await foreach (var directory in directoryResult.WithCancellation(token).ConfigureAwait(false))
            {
                yield return directory;
            }
        }

        public bool IsFileContentSearch(string actionKeyword) => actionKeyword == Settings.FileContentSearchActionKeyword;


        private bool UseWindowsIndexForDirectorySearch(string locationPath)
        {
            var pathToDirectory = FilesFolders.ReturnPreviousDirectoryIfIncompleteString(locationPath);

            return !Settings.IndexSearchExcludedSubdirectoryPaths.Any(
                       x => FilesFolders.ReturnPreviousDirectoryIfIncompleteString(pathToDirectory).StartsWith(x.Path, StringComparison.OrdinalIgnoreCase))
                   && WindowsIndex.WindowsIndex.PathIsIndexed(pathToDirectory);
        }

        private static bool IsEnvironmentVariableSearch(string search)
        {
            return search.StartsWith("%")
                   && search != "%%"
                   && !search.Contains('\\');
        }
    }

    [Flags]
    internal enum SearchTask
    {
        None = 0,
        IndexSearch = 1 << 0,
        QuickAccessSearch = 1 << 1,
        PathSearch = 1 << 2,
        FileContentSearch = 1 << 3,
    }
}
