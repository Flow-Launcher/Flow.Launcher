using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Flow.Launcher.Plugin.Explorer.Settings;

namespace Flow.Launcher.Plugin.Explorer.Search;
public  class ActionKeywordActive
{
    public string Keyword { get; }

    public Settings.ActionKeyword Type { get; }


    public ActionKeywordActive(string keyword, Settings.ActionKeyword type)
    {
        Keyword = keyword;
        Type = type;
    }

    public bool Equals(ActionKeyword type)
    {
        return Type == type;
    }

}
