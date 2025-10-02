using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage;
public class LastOpenedHistoryItem
{
    public string Title { get; set; } = string.Empty;
    public string SubTitle { get; set; } = string.Empty;
    public string IcoPath { get; set; } = string.Empty;
    public string PluginID { get; set; } = string.Empty;
    public Query OriginQuery { get; set; } = null!;
    public DateTime ExecutedDateTime { get; set; }
}
