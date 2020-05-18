using Flow.Launcher.Infrastructure.Logger;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.OleDb;
using System.Diagnostics;

namespace Flow.Launcher.Plugin.Explorer.Search.WindowsIndex
{
    internal class Searcher
    {
        public OleDbConnection conn;

        public OleDbCommand command;
        
        public OleDbDataReader dataReaderResults;

        private PluginInitContext _context;
        
        private readonly object _lock = new object();

        public Searcher(PluginInitContext context)
        {
            _context = context;
        }

        internal List<Result> ExecuteWindowsIndexSearch(string query, string connectionString)
        {
            var results = new List<Result>();

            try
            {
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
                                        results.Add(CreateResult(dataReaderResults));
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

        private Result CreateResult(OleDbDataReader dataReaderResults)
        {
            return new Result
            {
                Title = dataReaderResults.GetString(0),
                SubTitle = dataReaderResults.GetString(1),
                IcoPath = "Images\\Explorer.png",//<------CHANGE
                Action = c =>
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = dataReaderResults.GetString(1),
                            UseShellExecute = true,
                            //WorkingDirectory = workingDir <----??
                        });
                    }
                    catch (Win32Exception)
                    {
                        _context.API.ShowMsg("Explorer plugin: ", "Unable to open the selected file", string.Empty); //<=========
                    }

                    return true;
                }
            };
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
