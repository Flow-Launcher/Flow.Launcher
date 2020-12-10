using Flow.Launcher.Infrastructure.UserSettings;
using System;
using System.Collections.Generic;
using System.Text;

namespace Flow.Launcher.Plugin.PluginsManager
{
    internal class ContextMenu : IContextMenu
    {
        private PluginInitContext Context { get; set; }

        private Settings Settings { get; set; }

        public ContextMenu(PluginInitContext context, Settings settings)
        {
            Context = context;
            Settings = settings;
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            // Open website
            // Go to source code
            // Report an issue?
            // Request a feature?
            return new List<Result>();
        }
    }
}
