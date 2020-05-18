using Microsoft.Search.Interop;

namespace Flow.Launcher.Plugin.Explorer.Search.WindowsIndex
{
    public class QueryConstructor
    {
        private Settings _settings;

        private const string SystemIndex = "SystemIndex";

        public QueryConstructor(Settings settings)
        {
            _settings = settings;
        }

        public CSearchQueryHelper CreateBaseQuery()
        {
            var baseQuery = CreateQueryHelper();

            // Set the number of results we want. Don't set this property if all results are needed.
            baseQuery.QueryMaxResults = _settings.MaxResult;

            // Set list of columns we want to display, getting the path presently
            baseQuery.QuerySelectColumns = "System.FileName, System.ItemPathDisplay";

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
            return $"directory='file:{path}'";
        }

        ///<summary>
        /// Search will be performed on all folders and files on the first level of a specified directory.
        ///</summary>
        public string QueryForTopLevelDirectorySearch(string folderPath)
        {
            string query = "SELECT TOP " + _settings.MaxResult + $" {CreateBaseQuery().QuerySelectColumns} FROM {SystemIndex} WHERE ";

            query += QueryWhereRestrictionsForTopLevelDirectorySearch(folderPath);

            return query;
        }

        ///<summary>
        /// Search will be performed on all folders and files based on user's search keywords.
        ///</summary>
        public string QueryForAllFilesAndFolders(string userSearchString)
        {
            // Generate SQL from constructed parameters, converting the userSearchString from AQS->WHERE clause
            return CreateBaseQuery().GenerateSQLFromUserQuery(userSearchString) + " AND " + QueryWhereRestrictionsForAllFilesAndFoldersSearch();
        }

        ///<summary>
        /// Set the required WHERE clause restriction to search for all files and folders.
        ///</summary>
        public string QueryWhereRestrictionsForAllFilesAndFoldersSearch()
        {
            return $"scope='file:'";
        }
    }
}
