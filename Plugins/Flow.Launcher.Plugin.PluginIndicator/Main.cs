﻿using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Flow.Launcher.Core.Plugin;

namespace Flow.Launcher.Plugin.PluginIndicator
{
    public class Main : IPlugin, IPluginI18n
    {
        private PluginInitContext context;

        public List<Result> Query(Query query)
        {
            var results = from keyword in PluginManager.NonGlobalPlugins.Keys
                          where keyword.StartsWith(query.Terms[0])
                          from metadata in 
                              from plugin in PluginManager.NonGlobalPlugins[keyword]
                              select plugin.Metadata
                          where !metadata.Disabled
                          select new Result
                          {
                              Title = keyword,
                              SubTitle = $"Activate {metadata.Name} plugin",
                              Score = 100,
                              IcoPath = metadata.IcoPath,
                              Action = c =>
                              {
                                  context.API.ChangeQuery($"{keyword}{Plugin.Query.TermSeperater}");
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
