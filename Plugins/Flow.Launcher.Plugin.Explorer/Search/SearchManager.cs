using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using Flow.Launcher.Plugin.Explorer.Exceptions;
using Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo;
using Flow.Launcher.Plugin.Explorer.Search.Everything;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using Flow.Launcher.Plugin.SharedCommands;
using static Flow.Launcher.Plugin.Explorer.Settings;
using Path = System.IO.Path;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public class SearchManager
    {
        internal PluginInitContext Context;

        internal Settings Settings;

        private readonly Dictionary<ActionKeyword, List<ResultType>> _typesToFilterByActionKeyword = new()
        {
            { ActionKeyword.FileSearchActionKeyword, [ResultType.Folder, ResultType.Volume] },
            { ActionKeyword.FolderSearchActionKeyword, [ResultType.File] },
        };


        public SearchManager(Settings settings, PluginInitContext context)
        {
            Context = context;
            Settings = settings;
        }

        /// <summary>
        /// Note: A path that ends with "\" and one that doesn't will not be regarded as equal.
        /// </summary>
        public class PathEqualityComparator : IEqualityComparer<Result>
        {
            private static PathEqualityComparator instance;
            public static PathEqualityComparator Instance => instance ??= new PathEqualityComparator();
             
            public bool Equals(Result x, Result y)
            {
                return x.Title.Equals(y.Title, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.SubTitle, y.SubTitle, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(Result obj)
            {
                return HashCode.Combine(obj.Title.ToLowerInvariant(), obj.SubTitle?.ToLowerInvariant() ?? "");
            }
        }

        internal async Task<List<Result>> SearchAsync(Query query, CancellationToken token)
        {
            var results = new HashSet<Result>(PathEqualityComparator.Instance);

            var keyword = query.ActionKeyword.Length == 0 ? Query.GlobalPluginWildcardSign : query.ActionKeyword;
            // No action keyword matched - plugin should not handle this query, return empty results.
            var activeActionKeywords = Settings.GetActiveActionKeywords(keyword);
            if (activeActionKeywords.Count == 0)
            {
                return [];
            }

            var isPathSearch = query.Search.IsLocationPathString()
                || EnvironmentVariables.IsEnvironmentVariableSearch(query.Search)
                || EnvironmentVariables.HasEnvironmentVar(query.Search);

            var queryIsEmpty = string.IsNullOrEmpty(query.Search);

            if (queryIsEmpty && activeActionKeywords.ContainsKey(ActionKeyword.QuickAccessActionKeyword))
            {
                return QuickAccess.AccessLinkListAll(query, Settings.QuickAccessLinks);
            }

            // If query is empty and active keyword is folder or search, return empty results.
            if (queryIsEmpty && (activeActionKeywords.ContainsKey(ActionKeyword.FolderSearchActionKeyword)
                || activeActionKeywords.ContainsKey(ActionKeyword.FileSearchActionKeyword)))
            {
                return [];
            }

            // When file search is active, do not include path search in the active keywords.
            // This prevents unwanted PathSearch results (e.g., drives or raw volume paths).
            if (isPathSearch && !activeActionKeywords.ContainsKey(ActionKeyword.PathSearchActionKeyword)
                             && !activeActionKeywords.ContainsKey(ActionKeyword.FileSearchActionKeyword))
            {
                activeActionKeywords.Add(ActionKeyword.PathSearchActionKeyword, keyword);
            }

            IAsyncEnumerable<SearchResult> searchResults;

            string engineName;

            switch (isPathSearch)
            {
                case true
                    when activeActionKeywords.ContainsKey(ActionKeyword.PathSearchActionKeyword)
                         || activeActionKeywords.ContainsKey(ActionKeyword.SearchActionKeyword):
                    results.UnionWith(await PathSearchAsync(query, token).ConfigureAwait(false));
                    return [.. results];

                case false
                    // Intentionally require enabling of Everything's content search due to its slowness
                    when activeActionKeywords.ContainsKey(ActionKeyword.FileContentSearchActionKeyword):
                    if (Settings.ContentIndexProvider is EverythingSearchManager && !Settings.EnableEverythingContentSearch)
                        return EverythingContentSearchResult(query);

                    searchResults = Settings.ContentIndexProvider.ContentSearchAsync("", query.Search, token);
                    engineName = Enum.GetName(Settings.ContentSearchEngine);
                    break;

                case true or false
                    when activeActionKeywords.ContainsKey(ActionKeyword.QuickAccessActionKeyword):
                    return QuickAccess.AccessLinkListMatched(query, Settings.QuickAccessLinks);


                case false
                    when CanUseIndexSearchByActionKeywords(activeActionKeywords):
                    searchResults = Settings.IndexProvider.SearchAsync(query.Search, token);
                    engineName = Enum.GetName(Settings.IndexSearchEngine);
                    break;
                default:
                    return [..results];

            }

            //Merge Quick Access Link results for non-path searches.
            results.UnionWith(GetQuickAccessResultsFilteredByActionKeyword(query, activeActionKeywords));
            try
            {
                await foreach (var search in searchResults.WithCancellation(token).ConfigureAwait(false))
                {
                    if (ShouldSkipResultByTypeAndActionKeyword(activeActionKeywords, search))
                    {
                        continue;
                    }
                    results.Add(ResultManager.CreateResult(query, search));
                }
            }
            catch (OperationCanceledException)
            {
                return [.. results];
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
                excludedPath => FilesFolders.PathContains(excludedPath.Path, r.SubTitle, allowEqual: true)));

            return [.. results];
        }

        private List<Result> EverythingContentSearchResult(Query query)
        {
            return
            [
                new()
                {
                    Title = Localize.flowlauncher_plugin_everything_enable_content_search(),
                    SubTitle = Localize.flowlauncher_plugin_everything_enable_content_search_tips(),
                    IcoPath = "Images/index_error.png",
                    Action = c =>
                    {
                        Settings.EnableEverythingContentSearch = true;
                        Context.API.ChangeQuery(query.TrimmedQuery, true);
                        return false;
                    }
                }
            ];
        }

        private async Task<List<Result>> PathSearchAsync(Query query, CancellationToken token = default)
        {
            var querySearch = query.Search;

            var results = new HashSet<Result>(PathEqualityComparator.Instance);

            if (EnvironmentVariables.IsEnvironmentVariableSearch(querySearch))
                return EnvironmentVariables.GetEnvironmentStringPathSuggestions(querySearch, query, Context);

            // Query is a location path with a full environment variable, eg. %appdata%\somefolder\, c:\users\%USERNAME%\downloads
            var needToExpand = EnvironmentVariables.HasEnvironmentVar(querySearch);
            var path = needToExpand ? Environment.ExpandEnvironmentVariables(querySearch) : querySearch;

            // if user uses the unix directory separator, we need to convert it to windows directory separator
            path = path.Replace(Constants.UnixDirectorySeparator, Constants.DirectorySeparator);

            // Check that actual location exists, otherwise directory search will throw directory not found exception
            if (!FilesFolders.ReturnPreviousDirectoryIfIncompleteString(path).LocationExists())
                return [.. results];

            var useIndexSearch = Settings.IndexSearchEngine is Settings.IndexSearchEngineOption.WindowsIndex
                                 && UseWindowsIndexForDirectorySearch(path);

            var retrievedDirectoryPath = FilesFolders.ReturnPreviousDirectoryIfIncompleteString(path);

            results.Add(retrievedDirectoryPath.EndsWith(":\\")
                ? ResultManager.CreateDriveSpaceDisplayResult(retrievedDirectoryPath, query.ActionKeyword, useIndexSearch)
                : ResultManager.CreateOpenCurrentFolderResult(retrievedDirectoryPath, query.ActionKeyword, useIndexSearch));

            if (token.IsCancellationRequested)
                return [.. results];

            IAsyncEnumerable<SearchResult> directoryResult;

            var recursiveIndicatorIndex = path.IndexOf('>');

            if (recursiveIndicatorIndex > 0 && Settings.PathEnumerationEngine != Settings.PathEnumerationEngineOption.DirectEnumeration)
            {
                directoryResult =
                    Settings.PathEnumerator.EnumerateAsync(
                        path[..recursiveIndicatorIndex].Trim(),
                        path[(recursiveIndicatorIndex + 1)..],
                        true,
                        token);

            }
            else
            {
                directoryResult = DirectoryInfoSearch.TopLevelDirectorySearch(query, path, token).ToAsyncEnumerable();
            }

            if (token.IsCancellationRequested)
                return [.. results];

            try
            {
                await foreach (var directory in directoryResult.WithCancellation(token).ConfigureAwait(false))
                {
                    results.Add(ResultManager.CreateResult(query, directory));
                }
            }
            catch (Exception e)
            {
                throw new SearchException(Enum.GetName(Settings.PathEnumerationEngine), e.Message, e);
            }


            return [.. results];
        }

        public bool IsFileContentSearch(string actionKeyword) => actionKeyword == Settings.FileContentSearchActionKeyword;

        public static bool UseIndexSearch(string path)
        {
            if (Main.Settings.IndexSearchEngine is not IndexSearchEngineOption.WindowsIndex)
                return false;

            // Check if the path is using windows index search
            var pathToDirectory = FilesFolders.ReturnPreviousDirectoryIfIncompleteString(path);

            return !Main.Settings.IndexSearchExcludedSubdirectoryPaths.Any(
                       x => FilesFolders.ReturnPreviousDirectoryIfIncompleteString(pathToDirectory).StartsWith(x.Path, StringComparison.OrdinalIgnoreCase))
                   && WindowsIndex.WindowsIndex.PathIsIndexed(pathToDirectory);
        }

        private bool UseWindowsIndexForDirectorySearch(string locationPath)
        {
            var pathToDirectory = FilesFolders.ReturnPreviousDirectoryIfIncompleteString(locationPath);

            return !Settings.IndexSearchExcludedSubdirectoryPaths.Any(
                       x => FilesFolders.ReturnPreviousDirectoryIfIncompleteString(pathToDirectory).StartsWith(x.Path, StringComparison.OrdinalIgnoreCase))
                   && WindowsIndex.WindowsIndex.PathIsIndexed(pathToDirectory);
        }

        private bool IsExcludedFile(SearchResult result)
        {
            string[] excludedFileTypes = Settings.ExcludedFileTypes.Split([','], StringSplitOptions.RemoveEmptyEntries);
            string fileExtension = Path.GetExtension(result.FullPath).TrimStart('.');

            return excludedFileTypes.Contains(fileExtension, StringComparer.OrdinalIgnoreCase);
        }

        private bool ShouldSkipResultByTypeAndActionKeyword(Dictionary<ActionKeyword, string> actions, SearchResult search)
        {
            // Is excluded file type
            if (search.Type == ResultType.File && IsExcludedFile(search))
            {
                return true;
            }
            return IsResultTypeFilteredByActionKeyword(search.Type, actions);

        }

        private List<Result> GetQuickAccessResultsFilteredByActionKeyword(Query query, Dictionary<ActionKeyword, string> actions)
        {
            var results = QuickAccess.AccessLinkListMatched(query, Settings.QuickAccessLinks) ?? [];
            if (results.Count == 0)
            {
                return results;
            }

            return results
                .Where(r => r.ContextData is SearchResult result
                       && !IsResultTypeFilteredByActionKeyword(result.Type, actions))
                .ToList();
        }
        private bool IsResultTypeFilteredByActionKeyword(ResultType type, Dictionary<ActionKeyword, string> actions)
        {
            foreach (var action in actions.Keys)
            {
                if (_typesToFilterByActionKeyword.TryGetValue(action, out var typesToFilter))
                {
                    return typesToFilter.Contains(type);
                }
            }

            return false;
        }

        private bool CanUseIndexSearchByActionKeywords(Dictionary<ActionKeyword, string> actions)
        {
            List<ActionKeyword> keysToUseSearch =
            [
                ActionKeyword.FileSearchActionKeyword,
                ActionKeyword.FolderSearchActionKeyword,
                ActionKeyword.IndexSearchActionKeyword,
                ActionKeyword.SearchActionKeyword
            ];
            foreach (var key in keysToUseSearch)
            {
                var contains = actions.ContainsKey(key);
                if (!contains) continue;
                return contains;
            }

            return false;
        }

    }
}
