using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.PluginIndicator
{
    public class Main : IPlugin, IPluginI18n, IHomeQuery
    {
        internal static PluginInitContext Context { get; private set; }

        public List<Result> Query(Query query)
        {
            return QueryResults(query);
        }

        public List<Result> HomeQuery()
        {
            return QueryResults();
        }

        private List<Result> QueryResults(Query query = null)
        {
            var nonGlobalPlugins = GetNonGlobalPlugins();
            var querySearch = query?.Search ?? string.Empty;

            var results =
                from keyword in nonGlobalPlugins.Keys
                let plugin = nonGlobalPlugins[keyword].Metadata
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

        private Dictionary<string, PluginPair> GetNonGlobalPlugins()
        {
            var nonGlobalPlugins = new Dictionary<string, PluginPair>();
            foreach (var plugin in Context.API.GetAllPlugins())
            {
                foreach (var actionKeyword in plugin.Metadata.ActionKeywords)
                {
                    // Skip global keywords
                    if (actionKeyword == Plugin.Query.GlobalPluginWildcardSign) continue;

                    // Skip dulpicated keywords
                    if (nonGlobalPlugins.ContainsKey(actionKeyword)) continue;

                    nonGlobalPlugins.Add(actionKeyword, plugin);
                }
            }
            return nonGlobalPlugins;
        }

        public void Init(PluginInitContext context)
        {
            Context = context;
        }

        public string GetTranslatedPluginTitle()
        {
            return Localize.flowlauncher_plugin_pluginindicator_plugin_name();
        }

        public string GetTranslatedPluginDescription()
        {
            return Localize.flowlauncher_plugin_pluginindicator_plugin_description();
        }
    }
}
