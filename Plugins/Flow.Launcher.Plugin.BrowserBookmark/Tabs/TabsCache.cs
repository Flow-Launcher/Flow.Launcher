using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Automation;
using static Flow.Launcher.Plugin.BrowserBookmark.Main;

namespace Flow.Launcher.Plugin.BrowserBookmark.Tabs;

/// <summary>
/// TabsCache keeps record of all known browser's tabs in a single web browser window.
/// </summary>
public class TabsCache
{
    private static readonly string ClassName = nameof(TabsCache);

    // UIA is unreliable. It may return zero elements or a subset of elements.
    // Thus removal of an AutomationElement from cache SHOULD NOT be done immediately but rather after a few times the element no longer exists.
    private static int _removeAtAge = 3;

    private readonly Lock _sync = new();
    private Dictionary<AutomationElement, Info> _elementToInfo = [];
    private SortedDictionary<int, AutomationElement> _indexToElement = [];
    public bool Valid { get; private set; } = false;

    private class Info(int index)
    {
        public int Index { get; init; } = index;
        public int Age { get; set; } = 0;
    }

    private static string TryName(AutomationElement element)
    {
        try
        {
            return element.Current.Name;
        }
        catch (Exception e)
        {
            return e.GetType().ToString();
        }
    }

    private static bool Destroyed(AutomationElement element)
    {
        try
        {
            var _ = element.Current.Name;
        }
        catch (ElementNotAvailableException)
        {
            return true;
        }
        return false;
    }

    public void Invalidate()
    {
        lock (_sync)
        {
            Valid = false;
        }
    }
    
    public List<AutomationElement> GetTabs()
    {
        lock (_sync)
        {
            return [.. _indexToElement.Values];
        }
    }

    public AutomationElement TryGetTab(int index)
    {
        lock (_sync)
        {
            Context.API.LogDebug(ClassName, $"TABS:Checking if tab {index} exists in the cache of {_elementToInfo.Count} size and indices between {_indexToElement.Keys.FirstOrDefault()} and {_indexToElement.Keys.LastOrDefault()}");

            if (_indexToElement.TryGetValue(index, out var tab))
            {
                return tab;
            }
            return null;
        }
    }

    public int UpdateTabs(int lastAssignedIndex, List<AutomationElement> actualTabs, out List<AutomationElement> removedTabs)
    {
        lock (_sync)
        {
            Context.API.LogDebug(ClassName, $"TABS:Start comparing {actualTabs.Count} actual tabs to {_elementToInfo.Count} tabs in the cache; new tabs will start from {lastAssignedIndex+1}");

            removedTabs = [];

            var tabsToRemove = _elementToInfo.Where(t => !actualTabs.Contains(t.Key)).Select(t => t.Key).ToList();
            var tabsToAdd = actualTabs.Where(t => !_elementToInfo.ContainsKey(t)).ToList();
            var tabsToRevive = _elementToInfo.Where(t => actualTabs.Contains(t.Key) && t.Value.Age > 0).ToList();

            foreach (var tabToRemove in tabsToRemove)
            {
                if (_elementToInfo.TryGetValue(tabToRemove, out var info))
                {
                    if (Destroyed(tabToRemove) || info.Age >= _removeAtAge)
                    {
                        Context.API.LogDebug(ClassName, $"TABS:Removing {TryName(tabToRemove)} from cache");
                        _elementToInfo.Remove(tabToRemove);
                        _indexToElement.Remove(info.Index);
                        removedTabs.Add(tabToRemove);
                    }
                    else
                    {
                        Context.API.LogDebug(ClassName, $"TABS:Aging {TryName(tabToRemove)} in cache (got age {info.Age + 1}, will be removed at age {_removeAtAge} or on ElementNotAvailableException)");
                        _elementToInfo[tabToRemove].Age++;
                    }
                }
            }

            foreach (var tabToAdd in tabsToAdd)
            {
                Context.API.LogDebug(ClassName, $"TABS:Adding {TryName(tabToAdd)} to cache");
                var newIndex = ++lastAssignedIndex;
                _elementToInfo[tabToAdd] = new Info(newIndex);
                _indexToElement[newIndex] = tabToAdd;
            }

            foreach (var tabToRevive in tabsToRevive)
            {
                Context.API.LogDebug(ClassName, $"TABS:Reset age of {TryName(tabToRevive.Key)} as it appeared again");
                _elementToInfo[tabToRevive.Key].Age = 0;
            }

            Valid = true;
            return lastAssignedIndex;
        }
    }
}
