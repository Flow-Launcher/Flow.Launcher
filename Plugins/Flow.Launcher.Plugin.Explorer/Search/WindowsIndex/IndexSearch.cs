using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Plugin.SharedCommands;
using Microsoft.Search.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.OleDb;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.Explorer.Search.WindowsIndex
{
    internal class IndexSearch
    {
        private readonly object _lock = new object();

        public OleDbConnection conn;

        public OleDbCommand command;
        
        public OleDbDataReader dataReaderResults;

        // Reserved keywords in oleDB
        private string ReservedStringPattern = @"^[\/\\\$\%]+$";

        internal List<Result> ExecuteWindowsIndexSearch(string searchString, string connectionString, Query query)
        {
            var results = new List<Result>();

            try
            {
                using (conn = new OleDbConnection(connectionString))
                {
                    conn.Open();

                    using (command = new OleDbCommand(searchString, conn))
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
            catch (InvalidOperationException)
            {
                // Internal error from ExecuteReader(): Connection closed.
            }
            catch (Exception ex)
            {
                Log.Info(ex.ToString());//UPDATE THIS LOGGING
            }

            return results;
        }

        private Result CreateResult(string filename, string path, string fileType, Query query)
        {
            if (fileType == "Directory")
                return ResultManager.CreateFolderResult(filename, path, path, query);
            else
            {
                return ResultManager.CreateFileResult(path, query);
            }
        }

        internal List<Result> WindowsIndexSearch(string searchString, string connectionString, Func<string, string> constructQuery, Query query)
        {
            var regexMatch = Regex.Match(searchString, ReservedStringPattern);

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
    }
}
