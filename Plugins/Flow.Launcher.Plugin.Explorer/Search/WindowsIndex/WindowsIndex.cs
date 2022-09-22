using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using Microsoft.Search.Interop;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Flow.Launcher.Plugin.Explorer.Exceptions;

namespace Flow.Launcher.Plugin.Explorer.Search.WindowsIndex
{
    internal static class WindowsIndex
    {

        // Reserved keywords in oleDB
        private static Regex _reservedPatternMatcher = new(@"^[`\@\＠\#\＃\＊\^,\&\＆\/\\\$\%_;\[\]]+$", RegexOptions.Compiled);

        private static async IAsyncEnumerable<SearchResult> ExecuteWindowsIndexSearchAsync(string indexQueryString, string connectionString, [EnumeratorCancellation] CancellationToken token)
        {
            await using var conn = new OleDbConnection(connectionString);
            await conn.OpenAsync(token);
            token.ThrowIfCancellationRequested();

            await using var command = new OleDbCommand(indexQueryString, conn);
            // Results return as an OleDbDataReader.
            OleDbDataReader dataReaderAttempt;
            try
            {
                dataReaderAttempt = await command.ExecuteReaderAsync(token) as OleDbDataReader;
            }
            catch (OleDbException e)
            {
                Log.Exception($"|WindowsIndex.ExecuteWindowsIndexSearchAsync|Failed to execute windows index search query: {indexQueryString}", e);
                yield break;
            }
            await using var dataReader = dataReaderAttempt;
            token.ThrowIfCancellationRequested();

            if (dataReader is not { HasRows: true })
            {
                yield break;
            }

            while (await dataReader.ReadAsync(token))
            {
                token.ThrowIfCancellationRequested();
                if (dataReader.GetValue(0) == DBNull.Value || dataReader.GetValue(1) == DBNull.Value)
                {
                    continue;
                }
                // # is URI syntax for the fragment component, need to be encoded so LocalPath returns complete path   
                var encodedFragmentPath = dataReader
                    .GetString(1)
                    .Replace("#", "%23", StringComparison.OrdinalIgnoreCase);

                var path = new Uri(encodedFragmentPath).LocalPath;

                yield return new SearchResult
                {
                    FullPath = path,
                    Type = dataReader.GetString(2) == "Directory" ? ResultType.Folder : ResultType.File,
                    WindowsIndexed = true
                };
            }

            // Initial ordering, this order can be updated later by UpdateResultView.MainViewModel based on history of user selection.
        }



        internal static IAsyncEnumerable<SearchResult> WindowsIndexSearchAsync(string connectionString,
            string search,
            CancellationToken token)
        {
            try
            {

                return _reservedPatternMatcher.IsMatch(search)
                    ? AsyncEnumerable.Empty<SearchResult>()
                    : ExecuteWindowsIndexSearchAsync(search, connectionString, token);
            }
            catch (InvalidOperationException e)
            {
                throw new SearchException("Windows Index", e.Message, e);
            }
            catch (COMException e)
            {
                var api = SearchManager.Context.API;

                throw new EngineNotAvailableException("Windows Index", 
                    api.GetTranslation("plugin_explorer_windowsSearchServiceFix"),
                    api.GetTranslation("plugin_explorer_windowsSearchServiceNotRunning"),
                    e);
            }
        }

        // TODO: Move to General Search Manager
        private static void RemoveResultsInExclusionList(List<SearchResult> results, IReadOnlyList<AccessLink> exclusionList, CancellationToken token)
        {
            var indexExclusionListCount = exclusionList.Count;

            if (indexExclusionListCount == 0)
                return;
            results.RemoveAll(searchResult =>
                exclusionList.Any(exclude => searchResult.FullPath.StartsWith(exclude.Path, StringComparison.OrdinalIgnoreCase))
            );
        }

        internal static bool PathIsIndexed(string path)
        {
            try
            {
                var csm = new CSearchManager();
                var indexManager = csm.GetCatalog("SystemIndex").GetCrawlScopeManager();
                return indexManager.IncludedInCrawlScope(path) > 0;
            }
            catch (COMException)
            {
                // Occurs because the Windows Indexing (WSearch) is turned off in services and unable to be used by Explorer plugin
                return false;
            }
        }

        // TODO: Use a custom exception to handle this
        private static List<Result> ResultForWindexSearchOff(string rawQuery)
        {
            var api = SearchManager.Context.API;

            return new List<Result>
            {
                new Result
                {
                    Title = api.GetTranslation("plugin_explorer_windowsSearchServiceNotRunning"),
                    SubTitle = api.GetTranslation("plugin_explorer_windowsSearchServiceFix"),
                    Action = c =>
                    {
                        SearchManager.Settings.WarnWindowsSearchServiceOff = false;

                        var pluginsManagerPlugin = api.GetAllPlugins().FirstOrDefault(x => x.Metadata.ID == "9f8f9b14-2518-4907-b211-35ab6290dee7");

                        var actionKeywordCount = pluginsManagerPlugin.Metadata.ActionKeywords.Count;

                        if (actionKeywordCount > 1)
                            LogException("PluginsManager's action keyword has increased to more than 1, this does not allow for determining the " +
                                         "right action keyword. Explorer's code for managing Windows Search service not running exception needs to be updated",
                                new InvalidOperationException());

                        if (MessageBox.Show(string.Format(api.GetTranslation("plugin_explorer_alternative"), Environment.NewLine),
                                api.GetTranslation("plugin_explorer_alternative_title"),
                                MessageBoxButton.YesNo) == MessageBoxResult.Yes
                            && actionKeywordCount == 1)
                        {
                            api.ChangeQuery($"{pluginsManagerPlugin.Metadata.ActionKeywords[0]} install everything");
                        }
                        else
                        {
                            // Clears the warning message because same query string will not alter the displayed result list
                            api.ChangeQuery(string.Empty);

                            api.ChangeQuery(rawQuery);
                        }

                        var mainWindow = Application.Current.MainWindow!;
                        mainWindow.Show();
                        mainWindow.Focus();

                        return false;
                    },
                    IcoPath = Constants.ExplorerIconImagePath
                }
            };
        }

        private static void LogException(string message, Exception e)
        {
#if DEBUG // Please investigate and handle error from index search
            throw e;
#else
            Log.Exception($"|Flow.Launcher.Plugin.Explorer.IndexSearch|{message}", e);
#endif
        }
    }
}
