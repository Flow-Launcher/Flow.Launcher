using Microsoft.Search.Interop;

namespace Flow.Launcher.Plugin.Explorer.Search.WindowsIndex
{
    public class QueryConstructor
    {
        private readonly Settings settings;

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
            var manager = new CSearchManager();

            // SystemIndex catalog is the default catalog in Windows
            var catalogManager = manager.GetCatalog(SystemIndex);

            // Get the ISearchQueryHelper which will help us to translate AQS --> SQL necessary to query the indexer
            var queryHelper = catalogManager.GetQueryHelper();
            
            return queryHelper;
        }

        ///<summary>
        /// Set the required WHERE clause restriction to search on the first level of a specified directory.
        ///</summary>
        public string QueryWhereRestrictionsForTopLevelDirectorySearch(string path)
        {
            var searchDepth = $"directory='file:";

            return QueryWhereRestrictionsFromLocationPath(path, searchDepth);
        }

        ///<summary>
        /// Set the required WHERE clause restriction to search all files and subfolders of a specified directory.
        ///</summary>
        public string QueryWhereRestrictionsForTopLevelDirectoryAllFilesAndFoldersSearch(string path)
        {
            var searchDepth = $"scope='file:";

            return QueryWhereRestrictionsFromLocationPath(path, searchDepth);
        }

        private string QueryWhereRestrictionsFromLocationPath(string path, string searchDepth)
        {
            if (path.EndsWith(Constants.DirectorySeperator))
                return searchDepth + $"{path}'";

            var indexOfSeparator = path.LastIndexOf(Constants.DirectorySeperator);

            var itemName = path.Substring(indexOfSeparator + 1);

            if (itemName.StartsWith(Constants.AllFilesFolderSearchWildcard))
                itemName = itemName.Substring(1);

            var previousLevelDirectory = path.Substring(0, indexOfSeparator);

            if (string.IsNullOrEmpty(itemName))
                return $"{searchDepth}{previousLevelDirectory}'";

            return $"(System.FileName LIKE '{itemName}%' OR CONTAINS(System.FileName,'\"{itemName}*\"',1033)) AND {searchDepth}{previousLevelDirectory}'";
        }

        ///<summary>
        /// Search will be performed on all folders and files on the first level of a specified directory.
        ///</summary>
        public string QueryForTopLevelDirectorySearch(string path)
        {
            string query = "SELECT TOP " + settings.MaxResult + $" {CreateBaseQuery().QuerySelectColumns} FROM {SystemIndex} WHERE ";

            if (path.LastIndexOf(Constants.AllFilesFolderSearchWildcard) > path.LastIndexOf(Constants.DirectorySeperator))
                return query + QueryWhereRestrictionsForTopLevelDirectoryAllFilesAndFoldersSearch(path) + QueryOrderByFileNameRestriction;

            return query + QueryWhereRestrictionsForTopLevelDirectorySearch(path) + QueryOrderByFileNameRestriction;
        }

        ///<summary>
        /// Search will be performed on all folders and files based on user's search keywords.
        ///</summary>
        public string QueryForAllFilesAndFolders(string userSearchString)
        {
            // Generate SQL from constructed parameters, converting the userSearchString from AQS->WHERE clause
            return CreateBaseQuery().GenerateSQLFromUserQuery(userSearchString) + " AND " + QueryWhereRestrictionsForAllFilesAndFoldersSearch
                + QueryOrderByFileNameRestriction;
        }

        ///<summary>
        /// Set the required WHERE clause restriction to search for all files and folders.
        ///</summary>
        public const string QueryWhereRestrictionsForAllFilesAndFoldersSearch = "scope='file:'";

        public const string QueryOrderByFileNameRestriction = " ORDER BY System.Search.Rank DESC";


        ///<summary>
        /// Search will be performed on all indexed file contents for the specified search keywords.
        ///</summary>
        public string QueryForFileContentSearch(string userSearchString)
        {
            string query = "SELECT TOP " + settings.MaxResult + $" {CreateBaseQuery().QuerySelectColumns} FROM {SystemIndex} WHERE ";

            return query + QueryWhereRestrictionsForFileContentSearch(userSearchString) + " AND " + QueryWhereRestrictionsForAllFilesAndFoldersSearch
                + QueryOrderByFileNameRestriction;
        }

        ///<summary>
        /// Set the required WHERE clause restriction to search within file content.
        ///</summary>
        public string QueryWhereRestrictionsForFileContentSearch(string searchQuery)
        {
            return $"FREETEXT('{searchQuery}')";
        }
    }
}
