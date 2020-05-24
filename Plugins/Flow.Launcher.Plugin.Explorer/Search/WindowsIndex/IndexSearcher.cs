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
    internal class IndexSearcher
    {
        private readonly object _lock = new object();

        public OleDbConnection conn;

        public OleDbCommand command;
        
        public OleDbDataReader dataReaderResults;

        private PluginInitContext _context;

        // Reserved keywords in oleDB
        private string ReservedStringPattern = @"^[\/\\\$\%]+$";

        public IndexSearcher(PluginInitContext context)
        {
            _context = context;
        }

        internal List<Result> ExecuteWindowsIndexSearch(string searchString, string connectionString)
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
                                                        dataReaderResults.GetString(2)));
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

        private Result CreateResult(string filename, string path, string fileType)
        {
            return new Result
            {
                Title = filename,
                SubTitle = path,
                IcoPath = fileType == "Directory" ? Constants.FolderImagePath : Constants.FileImagePath,
                Action = c =>
                {
                    try
                    {
                        FilesFolders.OpenPath(path);
                    }
                    catch (Win32Exception)
                    {
                        _context.API.ShowMsg("Explorer plugin: ", "Unable to open the selected file", string.Empty); //<=========
                    }

                    return true;
                }
            };
        }

        internal List<Result> WindowsIndexSearch(string searchString, string connectionString, Func<string, string> constructQuery)
        {
            var regexMatch = Regex.Match(searchString, ReservedStringPattern);

            if (regexMatch.Success)
                return new List<Result>();

            lock (_lock)
            {
                var constructedQuery = constructQuery(searchString);
                return ExecuteWindowsIndexSearch(constructedQuery, connectionString);
            }
        }

        internal bool PathIsIndexed(string path)
        {
            var csm = new CSearchManager();
            var indexManager = csm.GetCatalog("SystemIndex").GetCrawlScopeManager();

            return indexManager.IncludedInCrawlScope(path) == 0;
        }
    }
}
