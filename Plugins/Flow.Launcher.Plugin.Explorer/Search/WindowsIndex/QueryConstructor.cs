using System;
using System.Buffers;
using Microsoft.Search.Interop;

namespace Flow.Launcher.Plugin.Explorer.Search.WindowsIndex
{
    public class QueryConstructor
    {
        private Settings Settings { get; }

        private const string SystemIndex = "SystemIndex";

        public CSearchQueryHelper BaseQueryHelper { get; }

        public QueryConstructor(Settings settings)
        {
            Settings = settings;
            BaseQueryHelper = CreateBaseQuery();
        }


        private CSearchQueryHelper CreateBaseQuery()
        {
            var baseQuery = CreateQueryHelper();

            // Set the number of results we want. Don't set this property if all results are needed.
            baseQuery.QueryMaxResults = Settings.MaxResult;

            // Set list of columns we want to display, getting the path presently
            baseQuery.QuerySelectColumns = "System.FileName, System.ItemUrl, System.ItemType";

            // Filter based on file name
            baseQuery.QueryContentProperties = "System.FileName";

            // Set sorting order 
            //baseQuery.QuerySorting = "System.ItemType DESC";

            return baseQuery;
        }

        internal static CSearchQueryHelper CreateQueryHelper()
        {
            // This uses the Microsoft.Search.Interop assembly
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

            var query = $"SELECT TOP {Settings.MaxResult} {BaseQueryHelper.QuerySelectColumns} FROM {SystemIndex} WHERE {scopeConstraint} {queryConstraint} ORDER BY {FileName}";

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
            return $"{BaseQueryHelper.GenerateSQLFromUserQuery(userSearchString.ToString())} AND {RestrictionsForAllFilesAndFoldersSearch} ORDER BY {FileName}";
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
                $"SELECT TOP {Settings.MaxResult} {BaseQueryHelper.QuerySelectColumns} FROM {SystemIndex} WHERE {RestrictionsForFileContentSearch(userSearchString)} AND {RestrictionsForAllFilesAndFoldersSearch} ORDER BY {FileName}";

            return query;
        }

        ///<summary>
        /// Set the required WHERE clause restriction to search within file content.
        ///</summary>
        public static string RestrictionsForFileContentSearch(ReadOnlySpan<char> searchQuery) => $"FREETEXT('{searchQuery}')";
    }
}
