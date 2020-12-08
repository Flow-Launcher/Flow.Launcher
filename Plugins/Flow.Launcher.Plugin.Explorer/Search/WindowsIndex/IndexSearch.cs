using Flow.Launcher.Infrastructure.Logger;
using Microsoft.Search.Interop;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Explorer.Search.WindowsIndex
{
    internal class IndexSearch
    {
        private readonly object _lock = new object();

        private OleDbConnection conn;

        private OleDbCommand command;
        
        private OleDbDataReader dataReaderResults;

        private readonly ResultManager resultManager;

        // Reserved keywords in oleDB
        private readonly string reservedStringPattern = @"^[\/\\\$\%_]+$";

        internal IndexSearch(PluginInitContext context)
        {
            resultManager = new ResultManager(context);
        }

        internal async Task<List<Result>> ExecuteWindowsIndexSearch(string indexQueryString, string connectionString, Query query)
        {
            var folderResults = new List<Result>();
            var fileResults = new List<Result>();
            var results = new List<Result>();

            try
            {
                using (conn = new OleDbConnection(connectionString))
                {
                    conn.Open();

                    using (command = new OleDbCommand(indexQueryString, conn))
                    {
                        // Results return as an OleDbDataReader.
                        var updateToken = Main.updateToken;
                        using (dataReaderResults = await command.ExecuteReaderAsync(updateToken) as OleDbDataReader)
                        {
                            if (updateToken.IsCancellationRequested)
                                return new List<Result>();
                            if (dataReaderResults.HasRows)
                            {
                                while (dataReaderResults.Read())
                                {
                                    if (dataReaderResults.GetValue(0) != DBNull.Value && dataReaderResults.GetValue(1) != DBNull.Value)
                                    {
                                        // # is URI syntax for the fragment component, need to be encoded so LocalPath returns complete path   
                                        var encodedFragmentPath = dataReaderResults
                                                                    .GetString(1)
                                                                    .Replace("#", "%23", StringComparison.OrdinalIgnoreCase);
                                        
                                        var path = new Uri(encodedFragmentPath).LocalPath;

                                        if (dataReaderResults.GetString(2) == "Directory")
                                        {
                                            folderResults.Add(resultManager.CreateFolderResult(
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
                    }
                }
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
            return results.Concat(folderResults.OrderBy(x => x.Title)).Concat(fileResults.OrderBy(x => x.Title)).ToList(); ;
        }

        internal List<Result> WindowsIndexSearch(string searchString, string connectionString, Func<string, string> constructQuery, Query query)
        {
            var regexMatch = Regex.Match(searchString, reservedStringPattern);

            if (regexMatch.Success)
                return new List<Result>();

            lock (_lock)
            {
                var constructedQuery = constructQuery(searchString);
                return ExecuteWindowsIndexSearch(constructedQuery, connectionString, query).GetAwaiter().GetResult();
            }
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
