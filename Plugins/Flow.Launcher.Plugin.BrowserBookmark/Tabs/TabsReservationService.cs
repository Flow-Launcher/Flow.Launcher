using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Automation;
using BrowserTabs;
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using static Flow.Launcher.Plugin.BrowserBookmark.Main;
using static Flow.Launcher.Plugin.BrowserBookmark.Tabs.TabsTracker;

namespace Flow.Launcher.Plugin.BrowserBookmark.Tabs;

/// <summary>
/// TabsReservationService provides mapping between URLs and browser tabs.
/// 1. You get a token while registering an URL
/// 2. Later on you may replace token for corresponding browser tab after it appears
/// TabsReservationService also integrates with BrowserBookmark's query by injecting activation of tabs instead of OpenUrl (InjectExistingTabs).
/// </summary>
public class TabsReservationService : IDisposable
{
    private static readonly string ClassName = nameof(TabsReservationService);

    private readonly Lock _sync = new();

    private readonly Dictionary<int, TokenForNewTabHandling> _tokens = [];

    private readonly ConcurrentDictionary<string, BrowserTab> _urlToBrowserTab = [];
    private readonly ConcurrentDictionary<AutomationElement, string> _automationElementToUrl = [];

    private static TabsTracker _tabsTracker;

    public TabsReservationService()
    {
        _tabsTracker = new TabsTracker(this);
    }

    /// <summary>
    /// TokenForNewTab is kind of a promise that may be replaced for a real tab after it is finally created and available
    /// </summary>
    public class TokenForNewTab(int index)
    {
        public int Index { get; init; } = index;
    }

    /// <summary>
    /// TokenForNewTabHandling is a utility class for proper handling of tokens for new tabs (TokenForNewTab)
    /// </summary>
    private class TokenForNewTabHandling(TokenForNewTab token)
    {
        public TokenForNewTab Token { get; init; } = token;
        public int LastReturnedIndex { get; set; }
        public int RequestedStill { get; set; } = 1;
    }

    public void OpenUrlAndTrack(Settings settings, string url)
    {
        if (settings.ReuseTabs)
        {
            _tabsTracker.MakeSnapshot(RegisterToken, url);
        }

        Context.API.OpenUrl(url);
    }

    public List<Result> InjectExistingTabs(Settings settings, List<Result> results)
    {
        if (!settings.ReuseTabs)
        {
            return results;
        }

        foreach (var r in results)
        {
            var bookmarkUrl = ((BookmarkAttributes)r.ContextData).Url;
            if (_urlToBrowserTab.TryGetValue(bookmarkUrl, out var existingTab))
            {
                r.ContextData = existingTab;
                r.Action = c =>
                {
                    if (!existingTab.ActivateTab())
                    {
                        Context.API.LogError(ClassName, "TABS:Failed to activate a tab");
                        _urlToBrowserTab.Remove(bookmarkUrl, out _);
                        _automationElementToUrl.Remove(existingTab.AutomationElement, out _);

                        OpenUrlAndTrack(settings, bookmarkUrl);
                    }
                    return true;
                };
            }
        }
        return results;
    }

    private TokenForNewTab RegisterToken(int lastAssignedIndex)
    {
        lock (_sync)
        {
            if (_tokens.TryGetValue(lastAssignedIndex, out var tokenHandling))
            {
                ++tokenHandling.RequestedStill;
                return tokenHandling.Token;
            }

            var token = new TokenForNewTab(lastAssignedIndex);
            _tokens[lastAssignedIndex] = new TokenForNewTabHandling(token);
            return token;
        }
    }

    public AutomationElement TryToResolveToken(TokenForNewTab token, out TrackingInfo trackingInfo)
    {
        lock (_sync)
        {
            if (!_tokens.TryGetValue(token.Index, out var tokenHandling))
            {
                Context.API.LogError(ClassName, $"Trying to use an invalid token for index {token.Index}");
                trackingInfo = null;
                return null;
            }

            _tabsTracker.MakeSnapshot();

            int expectedIndex = Math.Max(tokenHandling.LastReturnedIndex, token.Index) + 1;
            var tab = _tabsTracker.TryGetTab(expectedIndex, out var foundInTrackingInfo);
            trackingInfo = foundInTrackingInfo;
            if (tab != null)
            {
                if (tokenHandling.RequestedStill <= 1)
                {
                    _tokens.Remove(token.Index, out var _);
                }
                else
                {
                    --tokenHandling.RequestedStill;
                    tokenHandling.LastReturnedIndex = expectedIndex;
                }
            }
            return tab;
        }
    }

    public void RegisterTab(string url, TrackingInfo trackingInfo, AutomationElement tab)
    {
        lock (_sync)
        {
            try
            {
                var currentTab = new BrowserTab
                {
                    Title = tab.Current.Name,
                    BrowserName = trackingInfo.ProcessName,
                    Hwnd = trackingInfo.ProcessMainWindowHandle,
                    AutomationElement = tab
                };

                Context.API.LogDebug(ClassName, $"TABS:{RuntimeIdToKey(currentTab.AutomationElement)}:Registering {url} as tab: {currentTab.Title}");
                _urlToBrowserTab[url] = currentTab;
                _automationElementToUrl[currentTab.AutomationElement] = url;

                // required to take the tab into account by Flow Launcher main UI search window
                Context.API.ReQuery();
            }
            catch (ElementNotAvailableException)
            {
                Context.API.LogDebug(ClassName, $"TABS:Tab became unavailable before registration for {url}");
            }
        }
    }

    public void UnregisterTabs(IEnumerable<AutomationElement> elements)
    {
        lock (_sync)
        {
            foreach (var element in elements)
            {
                if (_automationElementToUrl.TryGetValue(element, out var url))
                {
                    _urlToBrowserTab.Remove(url, out _);
                    _automationElementToUrl.Remove(element, out _);
                }
            }
        }
    }

    public void Dispose()
    {
        _tabsTracker.Dispose();
    }

    public void EnableTracking(bool reuseTabs)
    {
        _tabsTracker.EnableTracking(reuseTabs);
        if (reuseTabs)
        {
            lock (_sync)
            {
                _urlToBrowserTab.Clear();
                _automationElementToUrl.Clear();
            }
        }
    }
}
