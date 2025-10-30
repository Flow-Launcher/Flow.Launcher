using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Explorer.Search;
public  class ActionKeywordConfiguration
{
    public string Keyword { get; }

    public Settings.ActionKeyword Type { get; }

    public bool Enable { get; }

    public ActionKeywordConfiguration(string keyword, Settings.ActionKeyword type, bool enable)
    {
        Keyword = keyword;
        Type = type;
        Enable = enable;
    }

    public bool IsActive(Settings.ActionKeyword type)
        => Type == type && Enable;
}
