﻿using Flow.Launcher.Core.ExternalPlugins;
using Flow.Launcher.Infrastructure.UserSettings;
using System;
using System.Collections.Generic;
using System.Text;

namespace Flow.Launcher.Plugin.PluginsManager
{
    internal class ContextMenu : IContextMenu
    {
        private PluginInitContext Context { get; set; }

        public ContextMenu(PluginInitContext context)
        {
            Context = context;
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            if(selectedResult.ContextData is not UserPlugin pluginManifestInfo) 
                return new List<Result>();

            return new List<Result>
            {
                new Result
                {
                    Title = Context.API.GetTranslation("plugin_pluginsmanager_plugin_contextmenu_openwebsite_title"),
                    SubTitle = Context.API.GetTranslation("plugin_pluginsmanager_plugin_contextmenu_openwebsite_subtitle"),
                    IcoPath = selectedResult.IcoPath,
                    Action = _ => 
                    {
                        Context.API.OpenUrl(pluginManifestInfo.Website);
                        return true;
                    }
                },
                new Result
                {
                    Title = Context.API.GetTranslation("plugin_pluginsmanager_plugin_contextmenu_gotosourcecode_title"),
                    SubTitle = Context.API.GetTranslation("plugin_pluginsmanager_plugin_contextmenu_gotosourcecode_subtitle"),
                    IcoPath = "Images\\sourcecode.png",
                    Action = _ =>
                    {
                        Context.API.OpenUrl(pluginManifestInfo.UrlSourceCode);
                        return true;
                    }
                },
                new Result
                {
                    Title = Context.API.GetTranslation("plugin_pluginsmanager_plugin_contextmenu_newissue_title"),
                    SubTitle = Context.API.GetTranslation("plugin_pluginsmanager_plugin_contextmenu_newissue_subtitle"),
                    IcoPath = "Images\\request.png",
                    Action = _ =>
                    {
                        // standard UrlSourceCode format in PluginsManifest's plugins.json file: https://github.com/jjw24/Flow.Launcher.Plugin.Putty/tree/master
                        var link = pluginManifestInfo.UrlSourceCode.StartsWith("https://github.com") 
                                                                ? pluginManifestInfo.UrlSourceCode.Replace("/tree/master", "/issues/new/choose") 
                                                                : pluginManifestInfo.UrlSourceCode;

                        Context.API.OpenUrl(link);
                        return true;
                    }
                },
                new Result
                {
                    Title = Context.API.GetTranslation("plugin_pluginsmanager_plugin_contextmenu_pluginsmanifest_title"),
                    SubTitle = Context.API.GetTranslation("plugin_pluginsmanager_plugin_contextmenu_pluginsmanifest_subtitle"),
                    IcoPath = "Images\\manifestsite.png",
                    Action = _ =>
                    {
                        Context.API.OpenUrl("https://github.com/Flow-Launcher/Flow.Launcher.PluginsManifest");
                        return true;
                    }
                }
            };
        }
    }
}
