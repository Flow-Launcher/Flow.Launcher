using System;
using Microsoft.Search.Interop;

namespace Flow.Launcher.Plugin.Explorer.Search.WindowsIndex
{
    public class QueryConstructor
    {
        private Settings settings { get; }

        private const string SystemIndex = "SystemIndex";

        public QueryConstructor(Settings settings)
        {
            this.settings = settings;
        }

        public CSearchQueryHelper CreateBaseQuery()
        {
            var baseQuery = CreateQueryHelper();

            // Set the number of results we want. Don't set this property if all results are needed.
            baseQuery.QueryMaxResults = settings.MaxResult;

            // Set list of columns we want to display, getting the path presently
            baseQuery.QuerySelectColumns = "System.FileName, System.ItemUrl, System.ItemType";

            // Filter based on file name
            baseQuery.QueryContentProperties = "System.FileName";

            // Set sorting order 
            //baseQuery.QuerySorting = "System.ItemType DESC";

            return baseQuery;
        }

        internal CSearchQueryHelper CreateQueryHelper()
        {
            // This uses the Microsoft.Search.Interop assembly
            // Throws COMException if Windows Search service is not running/disabled, this needs to be caught
            var manager = new CSearchManager();

            // SystemIndex catalog is the default catalog in Windows
            var catalogManager = manager.GetCatalog(SystemIndex);

            // Get the ISearchQueryHelper which will help us to translate AQS --> SQL necessary to query the indexer
            var queryHelper = catalogManager.GetQueryHelper();

            return queryHelper;
        }

        public static string TopLevelDirectoryConstraint(ReadOnlySpan<char> path) => $"directory='file:{path}'";
        public static string RecursiveDirectoryConstraint(ReadOnlySpan<char> path) => $"scope='file:{path}'";

        
        ///<summary>
        /// Search will be performed on all folders and files on the first level of a specified directory.
        ///</summary>
        public string Directory(ReadOnlySpan<char> path, ReadOnlySpan<char> searchString = default, bool recursive = false)
        {
            var queryConstraint = searchString.IsWhiteSpace() ? "" : $"AND ({FileName} LIKE '{searchString}%' OR CONTAINS({FileName},'\"{searchString}*\"'))";

            var scopeConstraint = recursive
                ? RecursiveDirectoryConstraint(path)
                : TopLevelDirectoryConstraint(path);

            var query = $"SELECT TOP {settings.MaxResult} {CreateBaseQuery().QuerySelectColumns} FROM {SystemIndex} WHERE {scopeConstraint} {queryConstraint} ORDER BY {FileName}";

            return query;
        }

        ///<summary>
        /// Search will be performed on all folders and files based on user's search keywords.
        ///</summary>
        public string FilesAndFolders(ReadOnlySpan<char> userSearchString)
        {
            if (userSearchString.IsWhiteSpace())
                userSearchString = "*";

            // Generate SQL from constructed parameters, converting the userSearchString from AQS->WHERE clause
            return $"{CreateBaseQuery().GenerateSQLFromUserQuery(userSearchString.ToString())} AND {RestrictionsForAllFilesAndFoldersSearch} ORDER BY {FileName}";
        }

        ///<summary>
        /// Set the required WHERE clause restriction to search for all files and folders.
        ///</summary>
        public const string RestrictionsForAllFilesAndFoldersSearch = "scope='file:'";

        /// <summary>
        /// Order identifier: file name
        /// </summary>
        public const string FileName = "System.FileName";


        ///<summary>
        /// Search will be performed on all indexed file contents for the specified search keywords.
        ///</summary>
        public string FileContent(ReadOnlySpan<char> userSearchString)
        {
            string query =
                $"SELECT TOP {settings.MaxResult} {CreateBaseQuery().QuerySelectColumns} FROM {SystemIndex} WHERE {RestrictionsForFileContentSearch(userSearchString)} AND {RestrictionsForAllFilesAndFoldersSearch} ORDER BY {FileName}";

            return query;
        }

        ///<summary>
        /// Set the required WHERE clause restriction to search within file content.
        ///</summary>
        public static string RestrictionsForFileContentSearch(ReadOnlySpan<char> searchQuery) => $"FREETEXT('{searchQuery}')";
    }
}
