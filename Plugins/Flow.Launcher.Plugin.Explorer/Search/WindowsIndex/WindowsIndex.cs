using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using Microsoft.Search.Interop;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Flow.Launcher.Plugin.Explorer.Search.WindowsIndex
{
    internal static class WindowsIndex
    {

        // Reserved keywords in oleDB
        private const string ReservedStringPattern = @"^[`\@\#\^,\&\/\\\$\%_;\[\]]+$";

        private static async Task<List<SearchResult>> ExecuteWindowsIndexSearchAsync(string indexQueryString, string connectionString, Query query, CancellationToken token)
        {
            var results = new List<SearchResult>();

            try
            {
                await using var conn = new OleDbConnection(connectionString);
                await conn.OpenAsync(token);
                token.ThrowIfCancellationRequested();

                await using var command = new OleDbCommand(indexQueryString, conn);
                // Results return as an OleDbDataReader.
                await using var dataReaderResults = await command.ExecuteReaderAsync(token) as OleDbDataReader;
                token.ThrowIfCancellationRequested();

                if (dataReaderResults is { HasRows: true })
                {
                    while (await dataReaderResults.ReadAsync(token))
                    {
                        token.ThrowIfCancellationRequested();
                        if (dataReaderResults.GetValue(0) != DBNull.Value && dataReaderResults.GetValue(1) != DBNull.Value)
                        {
                            // # is URI syntax for the fragment component, need to be encoded so LocalPath returns complete path   
                            var encodedFragmentPath = dataReaderResults
                                .GetString(1)
                                .Replace("#", "%23", StringComparison.OrdinalIgnoreCase);

                            var path = new Uri(encodedFragmentPath).LocalPath;

                            results.Add(new SearchResult()
                            {
                                FullPath = path,
                                Type = dataReaderResults.GetString(2) == "Directory" ? ResultType.Folder : ResultType.File,
                                WindowsIndexed = true
                            });
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // return empty result when cancelled
                return results;
            }
            catch (InvalidOperationException e)
            {
                // Internal error from ExecuteReader(): Connection closed.
                LogException("Internal error from ExecuteReader()", e);
            }
            catch (Exception e)
            {
                LogException("General error from performing index search", e);
            }

            // Initial ordering, this order can be updated later by UpdateResultView.MainViewModel based on history of user selection.
            return results;
        }

        internal static async ValueTask<List<SearchResult>> WindowsIndexSearchAsync(
            string searchString,
            Func<CSearchQueryHelper> createQueryHelper,
            Func<string, string> constructQuery,
            List<AccessLink> exclusionList,
            Query query,
            CancellationToken token)
        {
            var regexMatch = Regex.Match(searchString, ReservedStringPattern);

            if (regexMatch.Success)
                return new();

            var constructedQuery = constructQuery(searchString);

            return
                await ExecuteWindowsIndexSearchAsync(constructedQuery, createQueryHelper().ConnectionString, query, token);
        }

        private static List<Result> RemoveResultsInExclusionList(List<Result> results, List<AccessLink> exclusionList, CancellationToken token)
        {
            var indexExclusionListCount = exclusionList.Count;

            if (indexExclusionListCount == 0)
                return results;

            var filteredResults = new List<Result>();

            for (var index = 0; index < results.Count; index++)
            {
                token.ThrowIfCancellationRequested();

                var excludeResult = false;

                for (var i = 0; i < indexExclusionListCount; i++)
                {
                    token.ThrowIfCancellationRequested();

                    if (results[index].SubTitle.StartsWith(exclusionList[i].Path, StringComparison.OrdinalIgnoreCase))
                    {
                        excludeResult = true;
                        break;
                    }
                }

                if (!excludeResult)
                    filteredResults.Add(results[index]);
            }

            return filteredResults;
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
                            api.ChangeQuery(string.Format("{0} install everything", pluginsManagerPlugin.Metadata.ActionKeywords[0]));
                        }
                        else
                        {
                            // Clears the warning message because same query string will not alter the displayed result list
                            api.ChangeQuery(string.Empty);

                            api.ChangeQuery(rawQuery);
                        }

                        var mainWindow = Application.Current.MainWindow;
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