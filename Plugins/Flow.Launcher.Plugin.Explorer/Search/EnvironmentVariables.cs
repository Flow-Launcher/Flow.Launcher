using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public static class EnvironmentVariables
    {
        internal static bool IsEnvironmentVariableSearch(string search)
        {
            return search.StartsWith("%") 
                    && search != "%%"
                    && !search.Contains("\\") &&
                    LoadEnvironmentStringPaths().Count > 0;
        }

        internal static Dictionary<string, string> LoadEnvironmentStringPaths()
        {
            var envStringPaths = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (DictionaryEntry special in Environment.GetEnvironmentVariables())
            {
                var path = special.Value.ToString();
                if (Directory.Exists(path))
                {
                    // we add a trailing slash to the path to make sure drive paths become valid absolute paths.
                    // for example, if %systemdrive% is C: we turn it to C:\
                    path = path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

                    // if we don't have an absolute path, we use Path.GetFullPath to get one.
                    // for example, if %homepath% is \Users\John we turn it to C:\Users\John
                    path = Path.IsPathFullyQualified(path) ? path : Path.GetFullPath(path);

                    // Variables are returned with a mixture of all upper/lower case. 
                    // Call ToLower() to make the results look consistent
                    envStringPaths.Add(special.Key.ToString().ToLower(), path);
                }
            }

            return envStringPaths;
        }

        internal static string TranslateEnvironmentVariablePath(string environmentVariablePath)
        {
            var envStringPaths = LoadEnvironmentStringPaths();
            var splitSearch = environmentVariablePath.Substring(1).Split("%");
            var exactEnvStringPath = splitSearch[0];

            // if there are more than 2 % characters in the query, don't bother
            if (splitSearch.Length == 2 && envStringPaths.ContainsKey(exactEnvStringPath))
            {
                var queryPartToReplace = $"%{exactEnvStringPath}%";
                var expandedPath = envStringPaths[exactEnvStringPath];
                // replace the %envstring% part of the query with its expanded equivalent
                return environmentVariablePath.Replace(queryPartToReplace, expandedPath);
            }

            return environmentVariablePath;
        }

        internal static List<Result> GetEnvironmentStringPathSuggestions(string querySearch, Query query, PluginInitContext context)
        {
            var results = new List<Result>();

            var environmentVariables = LoadEnvironmentStringPaths();
            var search = querySearch;

            if (querySearch.EndsWith("%") && search.Length > 1)
            {
                // query starts and ends with a %, find an exact match from env-string paths
                search = querySearch.Substring(1, search.Length - 2);

                if (environmentVariables.ContainsKey(search))
                {
                    var expandedPath = environmentVariables[search];
                   
                    results.Add(ResultManager.CreateFolderResult($"%{search}%", expandedPath, expandedPath, query));
                    
                    return results;
                }
            }

            if (querySearch == "%")
            {
                search = ""; // Get all paths
            }
            else
            {
                search = search.Substring(1);
            }
            
            foreach (var p in environmentVariables)
            {
                if (p.Key.StartsWith(search, StringComparison.InvariantCultureIgnoreCase))
                {
                    results.Add(ResultManager.CreateFolderResult($"%{p.Key}%", p.Value, p.Value, query));
                }
            }

            return results;
        }
    }
}
