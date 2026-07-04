using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.PluginIndicator
{
    public class Main : IPlugin, IPluginI18n, IHomeQuery
    {
        internal static PluginInitContext Context { get; private set; }

        public void Init(PluginInitContext context)
        {
            Context = context;
        }

        public List<Result> Query(Query query)
        {
            return QueryResults(query);
        }

        private static List<Result> QueryResults(Query query = null)
        {
            var nonGlobalPlugins = GetNonGlobalPlugins();
            var querySearch = query?.Search ?? string.Empty;

            var results =
                from keyword in nonGlobalPlugins.Keys
                from pluginPair in nonGlobalPlugins[keyword]
                let plugin = pluginPair.Metadata
                let keywordSearchResult = Context.API.FuzzySearch(querySearch, keyword)
                let searchResult = keywordSearchResult.IsSearchPrecisionScoreMet() ? keywordSearchResult : Context.API.FuzzySearch(querySearch, plugin.Name)
                let score = searchResult.Score
                where (searchResult.IsSearchPrecisionScoreMet()
                        || string.IsNullOrEmpty(querySearch)) // To list all available action keywords
                    && !plugin.Disabled
                select new Result
                {
                    Title = keyword,
                    SubTitle = Localize.flowlauncher_plugin_pluginindicator_result_subtitle(plugin.Name),
                    Score = score,
                    IcoPath = plugin.IcoPath,
                    AutoCompleteText = $"{keyword}{Plugin.Query.TermSeparator}",
                    Action = c =>
                    {
                        Context.API.ChangeQuery($"{keyword}{Plugin.Query.TermSeparator}");
                        return false;
                    }
                };
            return [.. results];
        }

        private static Dictionary<string, List<PluginPair>> GetNonGlobalPlugins()
        {
            var nonGlobalPlugins = new Dictionary<string, List<PluginPair>>();
            foreach (var plugin in Context.API.GetAllPlugins())
            {
                foreach (var actionKeyword in plugin.Metadata.ActionKeywords)
                {
                    // Skip global keywords
                    if (actionKeyword == Plugin.Query.GlobalPluginWildcardSign) continue;

                    // See if we already assigned plugins to this keyword
                    if (!nonGlobalPlugins.TryGetValue(actionKeyword, out var pluginsForKeyword))
                    {
                        pluginsForKeyword = [];
                        nonGlobalPlugins[actionKeyword] = pluginsForKeyword;
                    }

                    // We allow the same keyword to have multiple different plugins and
                    // there is no need to check for the same plugin having the same keyword multiple times,
                    // as plugin manager and UI should prevent this - we can still display this state regardless
                    pluginsForKeyword.Add(plugin);
                }
            }
            return nonGlobalPlugins;
        }

        public string GetTranslatedPluginTitle()
        {
            return Localize.flowlauncher_plugin_pluginindicator_plugin_name();
        }

        public string GetTranslatedPluginDescription()
        {
            return Localize.flowlauncher_plugin_pluginindicator_plugin_description();
        }

        public List<Result> HomeQuery()
        {
            return QueryResults();
        }
    }
}
