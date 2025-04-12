using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public static class EnvironmentVariables
    {
        private static Dictionary<string, string> _envStringPaths = null;
        private static Dictionary<string, string> EnvStringPaths
        {
            get
            {
                if (_envStringPaths == null)
                {
                    LoadEnvironmentStringPaths();
                }
                return _envStringPaths;
            }
        }

        internal static bool IsEnvironmentVariableSearch(string search)
        {
            return search.StartsWith("%")
                    && search != "%%"
                    && !search.Contains('\\')
                    && EnvStringPaths.Count > 0;
        }

        public static bool HasEnvironmentVar(string search)
        {
            // "c:\foo %appdata%\" returns false
            var splited = search.Split(Path.DirectorySeparatorChar);
            return splited.Any(dir => dir.StartsWith('%') && 
                                        dir.EndsWith('%') &&
                                        dir.Length > 2 &&
                                        dir.Split('%').Length == 3);
        }

        private static void LoadEnvironmentStringPaths()
        {
            _envStringPaths = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            var homedrive = Environment.GetEnvironmentVariable("HOMEDRIVE")?.EnsureTrailingSlash() ?? "C:\\";

            foreach (DictionaryEntry special in Environment.GetEnvironmentVariables())
            {
                var path = special.Value.ToString();
                // we add a trailing slash to the path to make sure drive paths become valid absolute paths.
                // for example, if %systemdrive% is C: we turn it to C:\
                path = path.EnsureTrailingSlash();

                // if we don't have an absolute path, we use Path.GetFullPath to get one.
                // for example, if %homepath% is \Users\John we turn it to C:\Users\John
                // Add basepath for GetFullPath() to parse %HOMEPATH% correctly
                path = Path.IsPathFullyQualified(path) ? path : Path.GetFullPath(path, homedrive);

                if (Directory.Exists(path))
                {
                    // Variables are returned with a mixture of all upper/lower case. 
                    // Call ToUpper() to make the results look consistent
                    _envStringPaths.Add(special.Key.ToString().ToUpper(), path);
                }
            }
        }

        internal static List<Result> GetEnvironmentStringPathSuggestions(string querySearch, Query query, PluginInitContext context)
        {
            var results = new List<Result>();

            var search = querySearch;

            if (querySearch.EndsWith("%") && search.Length > 1)
            {
                // query starts and ends with a %, find an exact match from env-string paths
                search = querySearch.Substring(1, search.Length - 2);

                if (EnvStringPaths.ContainsKey(search))
                {
                    var expandedPath = EnvStringPaths[search];

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

            foreach (var p in EnvStringPaths)
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
