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
using Path = System.IO.Path;

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

            IAsyncEnumerable<SearchResult> searchResults;

            bool isPathSearch = query.Search.IsLocationPathString() 
                || EnvironmentVariables.IsEnvironmentVariableSearch(query.Search)
                || EnvironmentVariables.HasEnvironmentVar(query.Search);

            string engineName;

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
                    engineName = Enum.GetName(Settings.ContentSearchEngine);
                    break;

                case false
                    when ActionKeywordMatch(query, Settings.ActionKeyword.IndexSearchActionKeyword)
                         || ActionKeywordMatch(query, Settings.ActionKeyword.SearchActionKeyword):

                    searchResults = Settings.IndexProvider.SearchAsync(query.Search, token);
                    engineName = Enum.GetName(Settings.IndexSearchEngine);
                    break;
                default:
                    return results.ToList();
            }

            try
            {
                await foreach (var search in searchResults.WithCancellation(token).ConfigureAwait(false))
                    if (search.Type == ResultType.File && IsExcludedFile(search)) {
                        continue;
                    } else {
                        results.Add(ResultManager.CreateResult(query, search));
                    }
            }
            catch (OperationCanceledException)
            {
                return new List<Result>();
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

        private List<Result> EverythingContentSearchResult(Query query)
        {
            return new List<Result>()
            {
                new()
                {
                    Title = Context.API.GetTranslation("flowlauncher_plugin_everything_enable_content_search"),
                    SubTitle = Context.API.GetTranslation("flowlauncher_plugin_everything_enable_content_search_tips"),
                    IcoPath = "Images/index_error.png",
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

            if (EnvironmentVariables.IsEnvironmentVariableSearch(querySearch))
                return EnvironmentVariables.GetEnvironmentStringPathSuggestions(querySearch, query, Context);

            // Query is a location path with a full environment variable, eg. %appdata%\somefolder\, c:\users\%USERNAME%\downloads
            var needToExpand = EnvironmentVariables.HasEnvironmentVar(querySearch);
            var path = needToExpand ? Environment.ExpandEnvironmentVariables(querySearch) : querySearch;

            // Check that actual location exists, otherwise directory search will throw directory not found exception
            if (!FilesFolders.ReturnPreviousDirectoryIfIncompleteString(path).LocationExists())
                return results.ToList();

            var useIndexSearch = Settings.IndexSearchEngine is Settings.IndexSearchEngineOption.WindowsIndex
                                 && UseWindowsIndexForDirectorySearch(path);

            var retrievedDirectoryPath = FilesFolders.ReturnPreviousDirectoryIfIncompleteString(path);

            results.Add(retrievedDirectoryPath.EndsWith(":\\")
                ? ResultManager.CreateDriveSpaceDisplayResult(retrievedDirectoryPath, query.ActionKeyword, useIndexSearch)
                : ResultManager.CreateOpenCurrentFolderResult(retrievedDirectoryPath, query.ActionKeyword, useIndexSearch));

            if (token.IsCancellationRequested)
                return new List<Result>();

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
                return new List<Result>();

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


            return results.ToList();
        }

        public bool IsFileContentSearch(string actionKeyword) => actionKeyword == Settings.FileContentSearchActionKeyword;


        private bool UseWindowsIndexForDirectorySearch(string locationPath)
        {
            var pathToDirectory = FilesFolders.ReturnPreviousDirectoryIfIncompleteString(locationPath);

            return !Settings.IndexSearchExcludedSubdirectoryPaths.Any(
                       x => FilesFolders.ReturnPreviousDirectoryIfIncompleteString(pathToDirectory).StartsWith(x.Path, StringComparison.OrdinalIgnoreCase))
                   && WindowsIndex.WindowsIndex.PathIsIndexed(pathToDirectory);
        }

        private bool IsExcludedFile(SearchResult result)
        {
            string[] excludedFileTypes = Settings.ExcludedFileTypes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            string fileExtension = Path.GetExtension(result.FullPath).TrimStart('.');

            return excludedFileTypes.Contains(fileExtension, StringComparer.OrdinalIgnoreCase);
        }
    }
}
