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

    private static string RuntimeIdToKey(AutomationElement elem) {
        try
        {
            return elem != null ? string.Join("-", elem.GetRuntimeId()) : null;
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
    }

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
            var key = RuntimeIdToKey(tab);
            if (key != null)
            {
                _knownTabs.Add(key);
            }
        }
    }

    public void Add(IEnumerable<AutomationElement> tabs)
    {
        foreach (var tab in tabs)
        {
            Add(tab);
        }
    }

    public bool Contains(AutomationElement tab)
    {
        lock (sync)
        {
            var key = RuntimeIdToKey(tab);
            if (key != null)
            {
                return _knownTabs.Contains(key);
            }
        }
        return false;
    }
}
