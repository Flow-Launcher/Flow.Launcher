using System.Collections.Generic;
using System.Linq;
using System.Windows.Automation;
using static Flow.Launcher.Plugin.BrowserBookmark.Main;

namespace Flow.Launcher.Plugin.BrowserBookmark.Tabs;

/// <summary>
/// Keeps record of all known browser's tabs.
/// It is used by TabsWalker to identify new tabs as they appear.
/// </summary>
internal class TabsCache
{
    private static readonly string ClassName = nameof(TabsCache);
    private readonly HashSet<string> _knownTabs = [];
    private readonly object _sync = new();

    public static string RuntimeIdToKey(int[] runtimeId) => string.Join("-", runtimeId);

    public static string RuntimeIdToKey(AutomationElement elem) {
        try
        {
            return elem != null ? RuntimeIdToKey(elem.GetRuntimeId()) : null;
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
    }

    public bool Empty()
    {
        lock (_sync)
        {
            return _knownTabs.Count == 0;
        }
    }

    public void Add(AutomationElement tab)
    {
        lock (_sync)
        {
            var key = RuntimeIdToKey(tab);
            if (key != null)
            {
                Context.API.LogDebug(ClassName, $"Adding a tab to cache: {tab.Current.Name}");
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
        return Contains(RuntimeIdToKey(tab));
    }

    public bool Contains(string runtimeId)
    {
        lock (_sync)
        {
            if (runtimeId != null)
            {
                return _knownTabs.Contains(runtimeId);
            }
        }
        return false;
    }

    public void RemoveAllNonExistentTabs(AutomationElement rootElement, IEnumerable<AutomationElement> existingTabs)
    {
        if (rootElement == null || existingTabs == null)
            return;

        var rootKey = RuntimeIdToKey(rootElement);
        var existingKeys = existingTabs.Select(RuntimeIdToKey).Where(k => k != null).ToHashSet();
        var keysToRemove = _knownTabs.Where(t => t.StartsWith(rootKey) && !existingKeys.Contains(t));

        //Context.API.LogDebug(ClassName, $"Rootkey: {rootKey}");
        //Context.API.LogDebug(ClassName, $"Existing keys:\r\n{string.Join("\r\n", existingKeys)}");
        //Context.API.LogDebug(ClassName, $"Known Tabs:\r\n{string.Join("\r\n", _knownTabs)}");
        //Context.API.LogDebug(ClassName, $"Tabs to remove:\r\n{string.Join("\r\n", keysToRemove)}");

        foreach (var key in keysToRemove)
        {
            Context.API.LogDebug(ClassName, $"Removing a tab from cache: {key}");
            _knownTabs.Remove(key);
        }
    }
}
