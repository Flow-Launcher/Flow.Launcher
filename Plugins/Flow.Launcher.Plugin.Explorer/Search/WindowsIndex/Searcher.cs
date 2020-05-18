using System;
using System.Collections.Generic;
using System.Data.OleDb;

namespace Flow.Launcher.Plugin.Explorer.Search.WindowsIndex
{
    internal class Searcher
    {
        public OleDbConnection conn;

        public OleDbCommand command;
        
        public OleDbDataReader dataReaderResults;
        
        private readonly object _lock = new object();

        internal List<Result> ExecuteWindowsIndexSearch(string query, string connectionString)
        {
            var results = new List<Result>();

            using (conn = new OleDbConnection(connectionString))
            {
                conn.Open();

                using (command = new OleDbCommand(query, conn))
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
                                    var result = new Result
                                    {
                                        Title = dataReaderResults.GetString(0),
                                        SubTitle = dataReaderResults.GetString(1)
                                    };
                                    results.Add(result);
                                }
                            }
                        }
                    }
                }
            }

            return results;
        }

        internal List<Result> WindowsIndexSearch(string query, string connectionString, Func<string, string> constructQuery)
        {
            lock (_lock)
            {
                var constructedQuery = constructQuery(query);
                return ExecuteWindowsIndexSearch(constructedQuery, connectionString);
            }
        }
    }
}
