using System;
using System.Collections.Generic;
using System.Text;

namespace Flow.Launcher.Plugin.Explorer.Search.WindowsIndex
{
    public class QueryConstructor
    {
        private Settings _settings;

        public QueryConstructor(Settings settings)
        {
            _settings = settings;
        }

        ///<summary>
        /// Search will be performed on all folders and files on the first level of a specified directory.
        ///</summary>
        public string QueryWhereRestrictionsForTopLevelDirectorySearch(string path)
        {
            // Set query restriction for top level directory search
            return $"directory='file:{path}'";
        }

    }
}
