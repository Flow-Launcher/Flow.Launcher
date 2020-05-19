using System;
using System.Collections.Generic;
using System.Text;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public class SearchManager
    {
        public List<Result> TopLevelFolderSearch(
            Func<string, List<Result>> windowsIndexSearch,
            Func<string, List<Result>> directoryInfoClassSearch,
            Func<string, bool> indexExists,
            string path)
        {
            var results = windowsIndexSearch(path);

            if (results.Count == 0 && !indexExists(path))
                return directoryInfoClassSearch(path);

            return results;
        }
    }
}
