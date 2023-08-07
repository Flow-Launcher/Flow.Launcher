using Flow.Launcher.Infrastructure.Logger;
using Microsoft.Search.Interop;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
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
                if (dataReader.GetValue(0) is DBNull
                    || dataReader.GetValue(1) is not string rawFragmentPath
                    || string.Equals(rawFragmentPath, "file:", StringComparison.OrdinalIgnoreCase)
                    || dataReader.GetValue(2) is not string extension)
                {
                    continue;
                }
                // # is URI syntax for the fragment component, need to be encoded so LocalPath returns complete path   
                var encodedFragmentPath = rawFragmentPath.Replace("#", "%23", StringComparison.OrdinalIgnoreCase);

                var path = new Uri(encodedFragmentPath).LocalPath;

                yield return new SearchResult
                {
                    FullPath = path,
                    Type = string.Equals(extension, "Directory", StringComparison.Ordinal) ? ResultType.Folder : ResultType.File,
                    WindowsIndexed = true
                };
            }

            // Initial ordering, this order can be updated later by UpdateResultView.MainViewModel based on history of user selection.
        }

        internal static IAsyncEnumerable<SearchResult> WindowsIndexSearchAsync(
            string connectionString,
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
    }
}
