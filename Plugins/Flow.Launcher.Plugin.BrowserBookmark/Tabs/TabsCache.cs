using System.Collections.Generic;
using System.Windows.Automation;

namespace Flow.Launcher.Plugin.BrowserBookmark.Tabs;

/// <summary>
/// Keeps record of all known browser's tabs.
/// It is used by TabsWalker to identify new tabs as they appear.
/// </summary>
internal class TabsCache
{
    private readonly HashSet<string> _knownTabs = new();
    private readonly object sync = new();

    private static string RuntimeIdToKey(AutomationElement elem) => elem != null ? string.Join("-", elem.GetRuntimeId()) : "NULL";

    public bool Empty()
    {
        lock (sync)
        {
            return _knownTabs.Count == 0;
        }
    }

    public void Add(AutomationElement tab)
    {
        lock (sync)
        {
            _knownTabs.Add(RuntimeIdToKey(tab));
        }
    }

    public void Add(IEnumerable<AutomationElement> tabs)
    {
        lock (sync)
        {
            foreach (var tab in tabs)
            {
                _knownTabs.Add(RuntimeIdToKey(tab));
            }
        }
    }

    public bool Contains(AutomationElement tab)
    {
        lock (sync)
        {
            return _knownTabs.Contains(RuntimeIdToKey(tab));
        }
    }
}
