using Microsoft.Search.Interop;
using System;
using System.Collections.Generic;
using System.Text;

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
            // This uses the Microsoft.Search.Interop assembly
            CSearchManager manager = new CSearchManager();

            // SystemIndex catalog is the default catalog in Windows
            ISearchCatalogManager catalogManager = manager.GetCatalog(SystemIndex);

            // Get the ISearchQueryHelper which will help us to translate AQS --> SQL necessary to query the indexer
            var baseQuery = catalogManager.GetQueryHelper();

            // Set the number of results we want. Don't set this property if all results are needed.
            baseQuery.QueryMaxResults = _settings.MaxResult;

            // Set list of columns we want to display, getting the path presently
            baseQuery.QuerySelectColumns = "System.FileName, System.ItemPathDisplay";

            // Filter based on folder/file name
            baseQuery.QueryContentProperties = "System.FileName";

            // Set sorting order 
            //baseQuery.QuerySorting = "System.ItemType DESC";

            return baseQuery;
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

    }
}
