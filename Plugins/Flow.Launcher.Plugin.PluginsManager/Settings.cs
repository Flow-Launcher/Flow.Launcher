using System;
using System.Collections.Generic;
using System.Text;

namespace Flow.Launcher.Plugin.PluginsManager
{
    internal class Settings
    {
        internal string HotKeyInstall { get; set; } = "install";
        internal string HotkeyUninstall { get; set; } = "uninstall";

        internal string HotkeyUpdate { get; set; } = "update";
        
        internal readonly string icoPath = "Images\\pluginsmanager.png";


        internal List<Result> HotKeys
        {
            get
            {
                return  new List<Result>()
                {
                    new Result()
                    {
                        Title = HotKeyInstall,
                        IcoPath = icoPath,
                        Action = _ =>
                        {
                            Main.Context.API.ChangeQuery("pm install ");
                            return false;
                        }
                    },
                    new Result()
                    {
                        Title = HotkeyUninstall,
                        IcoPath = icoPath,
                        Action = _ =>
                        {
                            Main.Context.API.ChangeQuery("pm uninstall ");
                            return false;
                        }
                    },
                    new Result()
                    {
                        Title = HotkeyUpdate,
                        IcoPath = icoPath,
                        Action = _ =>
                        {
                            Main.Context.API.ChangeQuery("pm update ");
                            return false;
                        }
                    }
                };
            }
        }
    }
}