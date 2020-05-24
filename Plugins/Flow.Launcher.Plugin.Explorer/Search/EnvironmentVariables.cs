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
            return LoadEnvironmentStringPaths().Count > 0 && search.StartsWith("%") && !search.Substring(1).Contains("%");
        }

        internal static Dictionary<string, string> LoadEnvironmentStringPaths()
        {
            var envStringPaths = new Dictionary<string, string>();

            foreach (DictionaryEntry special in Environment.GetEnvironmentVariables())
            {
                if (Directory.Exists(special.Value.ToString()))
                {
                    envStringPaths.Add(special.Key.ToString().ToLower(), special.Value.ToString());
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

        internal static List<Result> GetEnvironmentStringPathSuggestions(string querySearch, Query query)
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
                   
                    results.Add(new ResultManager().CreateFolderResult($"%{search}%", expandedPath, expandedPath, query));
                    
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
                if (p.Key.StartsWith(search))
                {
                    results.Add(new ResultManager().CreateFolderResult($"%{p.Key}%", p.Value, p.Value, query));
                }
            }
            return results;
        }
    }
}
