using Flow.Launcher.Infrastructure.Logger;
using Microsoft.Search.Interop;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Text.RegularExpressions;

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
        private readonly string reservedStringPattern = @"^[\/\\\$\%]+$";

        internal IndexSearch(PluginInitContext context)
        {
            resultManager = new ResultManager(context);
        }

        internal List<Result> ExecuteWindowsIndexSearch(string indexQueryString, string connectionString, Query query)
        {
            var results = new List<Result>();

            try
            {
                using (conn = new OleDbConnection(connectionString))
                {
                    conn.Open();

                    using (command = new OleDbCommand(indexQueryString, conn))
                    {
                        // Results return as an OleDbDataReader.
                        using (dataReaderResults = command.ExecuteReader())
                        {
                            if (dataReaderResults.HasRows)
                            {
                                while (dataReaderResults.Read())
                                {
                                    if (dataReaderResults.GetValue(0) != DBNull.Value && dataReaderResults.GetValue(1) != DBNull.Value)
                                    {
                                        results.Add(CreateResult(
                                                        dataReaderResults.GetString(0), 
                                                        dataReaderResults.GetString(1), 
                                                        dataReaderResults.GetString(2),
                                                        query));
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

            return results;
        }

        private Result CreateResult(string filename, string path, string fileType, Query query)
        {
            if (fileType == "Directory")
                return resultManager.CreateFolderResult(filename, Constants.DefaultFolderSubtitleString, path, query, true, true);
            else
            {
                return resultManager.CreateFileResult(path, query, true, true);
            }
        }

        internal List<Result> WindowsIndexSearch(string searchString, string connectionString, Func<string, string> constructQuery, Query query)
        {
            var regexMatch = Regex.Match(searchString, reservedStringPattern);

            if (regexMatch.Success)
                return new List<Result>();

            lock (_lock)
            {
                var constructedQuery = constructQuery(searchString);
                return ExecuteWindowsIndexSearch(constructedQuery, connectionString, query);
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
