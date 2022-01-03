using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Core.Plugin;

namespace Flow.Launcher.Plugin.PluginIndicator
{
    public class Main : IPlugin, IPluginI18n
    {
        private PluginInitContext context;

        public List<Result> Query(Query query)
        {
            // if query contains more than one word, eg. github tips 
            // user has decided to type something else rather than wanting to see the available action keywords
            if (query.SearchTerms.Length > 1)
                return new List<Result>();

            var results = from keyword in PluginManager.NonGlobalPlugins.Keys
                          where keyword.StartsWith(query.SearchTerms[0])
                          let metadata = PluginManager.NonGlobalPlugins[keyword].Metadata
                          where !metadata.Disabled
                          select new Result
                          {
                              Title = keyword,
                              SubTitle = $"Activate {metadata.Name} plugin",
                              Score = 100,
                              IcoPath = metadata.IcoPath,
                              Action = c =>
                              {
                                  context.API.ChangeQuery($"{keyword}{Plugin.Query.TermSeparator}");
                                  return false;
                              }
                          };
            return results.ToList();
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;
        }

        public string GetTranslatedPluginTitle()
        {
            return context.API.GetTranslation("flowlauncher_plugin_pluginindicator_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return context.API.GetTranslation("flowlauncher_plugin_pluginindicator_plugin_description");
        }
    }
}
