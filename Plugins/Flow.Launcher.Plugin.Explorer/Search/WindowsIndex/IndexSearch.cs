using Flow.Launcher.Infrastructure.Logger;
using Microsoft.Search.Interop;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Explorer.Search.WindowsIndex
{
    internal class IndexSearch
    {
        private readonly ResultManager resultManager;

        // Reserved keywords in oleDB
        private readonly string reservedStringPattern = @"^[`\@\#\^,\&\/\\\$\%_]+$";

        internal IndexSearch(PluginInitContext context)
        {
            resultManager = new ResultManager(context);
        }

        internal async Task<List<Result>> ExecuteWindowsIndexSearchAsync(string indexQueryString, string connectionString, Query query, CancellationToken token)
        {
            var results = new List<Result>();
            var fileResults = new List<Result>();

            try
            {
                using var conn = new OleDbConnection(connectionString);
                await conn.OpenAsync(token);
                token.ThrowIfCancellationRequested();

                using var command = new OleDbCommand(indexQueryString, conn);
                // Results return as an OleDbDataReader.
                using var dataReaderResults = await command.ExecuteReaderAsync(token) as OleDbDataReader;
                token.ThrowIfCancellationRequested();

                if (dataReaderResults.HasRows)
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

                            if (dataReaderResults.GetString(2) == "Directory")
                            {
                                results.Add(resultManager.CreateFolderResult(
                                                                    dataReaderResults.GetString(0),
                                                                    path,
                                                                    path,
                                                                    query, true, true));
                            }
                            else
                            {
                                fileResults.Add(resultManager.CreateFileResult(path, query, true, true));
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return new List<Result>(); // The source code indicates that without adding members, it won't allocate an array
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

            results.AddRange(fileResults);

            // Intial ordering, this order can be updated later by UpdateResultView.MainViewModel based on history of user selection.
             return results;
        }

        internal async Task<List<Result>> WindowsIndexSearchAsync(string searchString, string connectionString,
                                                                  Func<string, string> constructQuery, Query query,
                                                                  CancellationToken token)
        {
            var regexMatch = Regex.Match(searchString, reservedStringPattern);

            if (regexMatch.Success)
                return new List<Result>();

            var constructedQuery = constructQuery(searchString);
            return await ExecuteWindowsIndexSearchAsync(constructedQuery, connectionString, query, token);

        }

        internal bool PathIsIndexed(string path)
        {
            var csm = new CSearchManager();
            var indexManager = csm.GetCatalog("SystemIndex").GetCrawlScopeManager();
            return indexManager.IncludedInCrawlScope(path) > 0;
        }

        private void LogException(string message, Exception e)
        {
#if DEBUG // Please investigate and handle error from index search
            throw e;
#else
            Log.Exception($"|Flow.Launcher.Plugin.Explorer.IndexSearch|{message}", e);
#endif            
        }
    }
}
