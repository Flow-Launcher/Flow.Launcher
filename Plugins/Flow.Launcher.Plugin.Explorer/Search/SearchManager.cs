using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        private readonly Dictionary<ActionKeyword, ResultType[]> _allowedTypesByActionKeyword = new()
        {
            { ActionKeyword.FileSearchActionKeyword, [ResultType.File] },
            { ActionKeyword.FolderSearchActionKeyword, [ResultType.Folder, ResultType.Volume] },
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

        /// <summary>
        /// Results for the different types of searches as follows: 
        /// 1. Search, only include results from:
        ///   - Files
        ///   - Folders
        ///   - Quick Access Links
        ///   - Path navigation
        /// 2. File Content Search, only include results from:
        ///   - File contents from index search engines i.e. Windows Index, Everything (may not be available due its beta version)
        /// 3. Path Search, only include results from:
        ///   - Path navigation
        /// 4. Quick Access Links, only include results from:
        ///   - Full list of Quick Access Links if query is empty
        ///   - Matched Quick Access Links if query is not empty
        ///   - Quick Access Links that are matched on path, e.g. query "window" for results that contain 'window' in the path (even if not in the title),
        ///     i.e. result with path c:\windows\system32
        /// 5. Folder Search, only include results from:
        ///   - Folders
        ///   - Quick Access Links
        /// 6. File Search, only include results from:
        ///   - Files
        ///   - Quick Access Links
        /// </summary>
        internal async Task<List<Result>> SearchAsync(Query query, CancellationToken token)
        {
            var results = new HashSet<Result>(PathEqualityComparator.Instance);

            var keyword = query.ActionKeyword.Length == 0 ? Query.GlobalPluginWildcardSign : query.ActionKeyword;

            // No action keyword matched - plugin should not handle this query, return empty results.
            var activeActionKeywords = Settings.GetActiveActionKeywords(keyword);
            if (activeActionKeywords.Count == 0)
            {
                return [.. results];
            }

            var queryIsEmpty = string.IsNullOrEmpty(query.Search);
            if (queryIsEmpty && activeActionKeywords.ContainsKey(ActionKeyword.QuickAccessActionKeyword))
            {
                return QuickAccess.AccessLinkListAll(query, Settings.QuickAccessLinks);
            }

            if (queryIsEmpty)
            {
                return [.. results];
            }

            var isPathSearch = query.Search.IsLocationPathString()
                || EnvironmentVariables.IsEnvironmentVariableSearch(query.Search)
                || EnvironmentVariables.HasEnvironmentVar(query.Search);

            IAsyncEnumerable<SearchResult> searchResults;

            string engineName;

            switch (isPathSearch)
            {
                case true
                    when CanUsePathSearchByActionKeywords(activeActionKeywords):
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
                    return [.. results];

            }


            var actions = activeActionKeywords.Keys.ToList();
            //Merge Quick Access Link results for non-path searches.
            results.UnionWith(GetQuickAccessResultsFilteredByActionKeyword(query, actions));
            try
            {
                await foreach (var search in searchResults.WithCancellation(token).ConfigureAwait(false))
                {
                    if (search.Type == ResultType.File && IsExcludedFile(search))
                        continue;

                    if (IsResultTypeFilteredByActionKeyword(search.Type, actions))
                        continue;

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

        private List<Result> GetQuickAccessResultsFilteredByActionKeyword(Query query, List<ActionKeyword> actions)
        {
            if (!Settings.QuickAccessKeywordEnabled)
                return [];

            var results = QuickAccess.AccessLinkListMatched(query, Settings.QuickAccessLinks);
            if (results.Count == 0)
                return [];

            return results
                .Where(r => r.ContextData is SearchResult result
                       && !IsResultTypeFilteredByActionKeyword(result.Type, actions))
                .ToList();
        }
        private bool IsResultTypeFilteredByActionKeyword(ResultType type, List<ActionKeyword> actions)
        {
            var actionsWithWhitelist = actions.Intersect(_allowedTypesByActionKeyword.Keys).ToList();
            if (actionsWithWhitelist.Count == 0) return false;

            // Check if ANY active keyword allows this type (union behavior)
            foreach (var action in actionsWithWhitelist)
            {
                if (_allowedTypesByActionKeyword.TryGetValue(action, out var allowedTypes))
                {
                    if (allowedTypes.Contains(type))
                        return false; 
                }
            }

            return true;
        }

        private bool CanUseIndexSearchByActionKeywords(Dictionary<ActionKeyword, string> actions)
        {
            var keysToUseIndexSearch = new[]
            {
                ActionKeyword.FileSearchActionKeyword, ActionKeyword.FolderSearchActionKeyword,
                ActionKeyword.IndexSearchActionKeyword, ActionKeyword.SearchActionKeyword
            };

            return keysToUseIndexSearch.Any(actions.ContainsKey);
        }

        // Action keywords that supports patch search in results.
        private bool CanUsePathSearchByActionKeywords(Dictionary<ActionKeyword, string> actions)
        {
            var keysThatSupportPathSearch = new[]
            {
                ActionKeyword.PathSearchActionKeyword,
                ActionKeyword.SearchActionKeyword,
            };

            return keysThatSupportPathSearch.Any(actions.ContainsKey);

        }

    }
}
