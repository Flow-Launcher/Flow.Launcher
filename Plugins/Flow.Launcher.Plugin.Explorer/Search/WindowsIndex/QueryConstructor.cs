using System;
using System.Text.RegularExpressions;
using Microsoft.Search.Interop;

namespace Flow.Launcher.Plugin.Explorer.Search.WindowsIndex
{
    public class QueryConstructor
    {
        private static readonly Regex _specialCharacterMatcher = new(@"[\@\＠\#\＃\&\＆＊_;,\%\|\!\(\)\{\}\[\]\^\~\?\\""\/\:\=\-]+", RegexOptions.Compiled);
        private static readonly Regex _multiWhiteSpacesMatcher = new(@"\s+", RegexOptions.Compiled);

        private Settings Settings { get; }

        private const string SystemIndex = "SystemIndex";

        public QueryConstructor(Settings settings)
        {
            Settings = settings;
        }

        public CSearchQueryHelper CreateBaseQuery()
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
            var queryConstraint = searchString.IsWhiteSpace() ? "" : $"AND (System.FileName LIKE '{searchString}%' OR CONTAINS(System.FileName,'\"{searchString}*\"'))";

            var scopeConstraint = recursive
                ? RecursiveDirectoryConstraint(path)
                : TopLevelDirectoryConstraint(path);

            var query = $"SELECT TOP {Settings.MaxResult} {CreateBaseQuery().QuerySelectColumns} FROM {SystemIndex} WHERE {scopeConstraint} {queryConstraint} ORDER BY {OrderIdentifier}";

            return query;
        }

        ///<summary>
        /// Search will be performed on all folders and files based on user's search keywords.
        ///</summary>
        public string FilesAndFolders(ReadOnlySpan<char> userSearchString)
        {
            if (userSearchString.IsWhiteSpace())
                userSearchString = "*";

            // Remove any special characters that might cause issues with the query
            var replacedSearchString = ReplaceSpecialCharacterWithTwoSideWhiteSpace(userSearchString);

            // Generate SQL from constructed parameters, converting the userSearchString from AQS->WHERE clause
            return $"{CreateBaseQuery().GenerateSQLFromUserQuery(replacedSearchString)} AND {RestrictionsForAllFilesAndFoldersSearch} ORDER BY {OrderIdentifier}";
        }

        /// <summary>
        /// If one special character have white space on one side, replace it with one white space.
        /// So command will not have "[special character]+*" which will cause OLEDB exception.
        /// </summary>
        private static string ReplaceSpecialCharacterWithTwoSideWhiteSpace(ReadOnlySpan<char> input)
        {
            const string whiteSpace = " ";

            var inputString = input.ToString();

            // Use regex to match special characters with whitespace on one side
            // and replace them with a single space
            var result = _specialCharacterMatcher.Replace(inputString, match =>
            {
                // Check if the match has whitespace on one side
                bool hasLeadingWhitespace = match.Index > 0 && char.IsWhiteSpace(inputString[match.Index - 1]);
                bool hasTrailingWhitespace = match.Index + match.Length < inputString.Length && char.IsWhiteSpace(inputString[match.Index + match.Length]);
                if (hasLeadingWhitespace || hasTrailingWhitespace)
                {
                    return whiteSpace;
                }
                return match.Value;
            });

            // Remove any extra spaces that might have been introduced
            return _multiWhiteSpacesMatcher.Replace(result, whiteSpace).Trim();
        }

        ///<summary>
        /// Set the required WHERE clause restriction to search for all files and folders.
        ///</summary>
        public const string RestrictionsForAllFilesAndFoldersSearch = "scope='file:'";

        /// <summary>
        /// Order identifier: System.Search.Rank DESC
        /// </summary>
        /// <remarks>
        /// <see href="https://docs.microsoft.com/en-us/windows/win32/properties/props-system-search-rank"/>
        /// </remarks>
        public const string OrderIdentifier = "System.Search.Rank DESC";

        ///<summary>
        /// Search will be performed on all indexed file contents for the specified search keywords.
        ///</summary>
        public string FileContent(ReadOnlySpan<char> userSearchString)
        {
            string query =
                $"SELECT TOP {Settings.MaxResult} {CreateBaseQuery().QuerySelectColumns} FROM {SystemIndex} WHERE {RestrictionsForFileContentSearch(userSearchString)} AND {RestrictionsForAllFilesAndFoldersSearch} ORDER BY {OrderIdentifier}";

            return query;
        }

        ///<summary>
        /// Set the required WHERE clause restriction to search within file content.
        ///</summary>
        public static string RestrictionsForFileContentSearch(ReadOnlySpan<char> searchQuery) => $"FREETEXT('{searchQuery}')";
    }
}
