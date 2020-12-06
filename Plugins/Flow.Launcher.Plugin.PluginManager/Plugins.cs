using System;
using System.Collections.Generic;
using System.Text;

namespace Flow.Launcher.Plugin.PluginsManager
{
    internal class Plugin
    {
        internal string ID { get; set; }
        internal string Name { get; set; }
        internal string Description { get; set; }
        internal string Author { get; set; }
        internal string Version { get; set; }
        internal string Language { get; set; }
        internal string Website { get; set; }
        internal string UrlDownload { get; set; }
        internal string UrlSourceCode { get; set; }
    }
}
