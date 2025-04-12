using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.PluginIndicator
{
    public class Main : IPlugin, IPluginI18n
    {
        internal PluginInitContext Context { get; private set; }

        public List<Result> Query(Query query)
        {
            var nonGlobalPlugins = GetNonGlobalPlugins();
            var results =
                from keyword in nonGlobalPlugins.Keys
                let plugin = nonGlobalPlugins[keyword].Metadata
                let keywordSearchResult = Context.API.FuzzySearch(query.Search, keyword)
                let searchResult = keywordSearchResult.IsSearchPrecisionScoreMet() ? keywordSearchResult : Context.API.FuzzySearch(query.Search, plugin.Name)
                let score = searchResult.Score
                where (searchResult.IsSearchPrecisionScoreMet()
                        || string.IsNullOrEmpty(query.Search)) // To list all available action keywords
                    && !plugin.Disabled
                select new Result
                {
                    Title = keyword,
                    SubTitle = string.Format(Context.API.GetTranslation("flowlauncher_plugin_pluginindicator_result_subtitle"), plugin.Name),
                    Score = score,
                    IcoPath = plugin.IcoPath,
                    AutoCompleteText = $"{keyword}{Plugin.Query.TermSeparator}",
                    Action = c =>
                    {
                        Context.API.ChangeQuery($"{keyword}{Plugin.Query.TermSeparator}");
                        return false;
                    }
                };
            return results.ToList();
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
            return Context.API.GetTranslation("flowlauncher_plugin_pluginindicator_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return Context.API.GetTranslation("flowlauncher_plugin_pluginindicator_plugin_description");
        }
    }
}
