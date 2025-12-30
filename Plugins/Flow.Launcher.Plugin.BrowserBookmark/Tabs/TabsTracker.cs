using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Automation;
using BrowserTabs;
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using static Flow.Launcher.Plugin.BrowserBookmark.Main;

namespace Flow.Launcher.Plugin.BrowserBookmark.Tabs;

/// <summary>
/// TabsTracker maps initial URLs into existing browser's tabs.
/// The sequence of events:
/// 1. OpenUrlAndTrack - before launching an URL it is remembered for later mapping to a browser's tab
/// 2. OnFocusChanged - whenever a browser's window gets focused a new tab discovery is started and result is put into the UrlToBrowserTab map
/// 3. InjectExistingTabs - iterates over BrowserBookmark's query result and replaces OpenUrl with ActivateTab for known, existing tabs
/// </summary>
public class TabsTracker : IDisposable
{
    private static readonly string ClassName = nameof(TabsTracker);
    private static readonly HashSet<string> chromiumProcessNames = new HashSet<string>(["msedge", "chrome", "brave", "vivaldi", "opera", "chromium"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> firefoxProcessNames = new HashSet<string>(["firefox"], StringComparer.OrdinalIgnoreCase);
    private readonly TabsWalker _walker = new();
    private readonly Queue<string> _expectedUrls = [];
    private Dictionary<string, BrowserTab> UrlToBrowserTab { get; } = [];
    private readonly object _sync = new();

    private TabsFocusEventDispatcher _focusHandlerDispatcher;
    private AutomationFocusChangedEventHandler? _focusHandler;
    private readonly HashSet<AutomationElement> _browserWindowsTracked = [];
    private bool _initialized;

    public void OpenUrlAndTrack(Settings settings, string url)
    {
        if (settings.ReuseTabs)
        {
            Context.API.LogDebug(ClassName, $"Opening... {url}");
            ExpectUrl(url);
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
            var existingTab = GetExistingTab(bookmarkUrl);
            if (existingTab != null)
            {
                Context.API.LogDebug(ClassName, $"Mapped {bookmarkUrl}");

                r.ContextData = existingTab;
                r.Action = c =>
                {
                    if (!existingTab.ActivateTab())
                    {
                        Context.API.LogError(ClassName, "Failed to activate a tab");
                        Remove(bookmarkUrl);
                        OpenUrlAndTrack(settings, bookmarkUrl);
                    }
                    return true;
                };
            }
        }
        return results;
    }

    public void Init()
    {
        if (_initialized)
            return;

        _focusHandlerDispatcher = new(_walker, this);
        _focusHandler = OnFocusChanged;
        Automation.AddAutomationFocusChangedEventHandler(_focusHandler);
        _initialized = true;
    }

    public void Dispose()
    {
        var windowsToUnsubscribe = _browserWindowsTracked.ToList();
        foreach (var wnd in windowsToUnsubscribe)
        {
            UnsubscribeStructureChangedForWindow(wnd);
        }
        if (_focusHandler != null)
        {
            Automation.RemoveAutomationFocusChangedEventHandler(_focusHandler);
            _focusHandler = null;
            _initialized = false;
        }
        _focusHandlerDispatcher?.Dispose();
    }

    public void ExpectUrl(string url)
    {
        lock (_sync)
        {
            _expectedUrls.Enqueue(url);
        }
    }

    private BrowserTab GetExistingTab(string url)
    {
        lock (_sync)
        {
            if (UrlToBrowserTab.TryGetValue(url, out var existingTab))
            {
                return existingTab;
            }
        }
        return null;
    }

    private void Remove(string url)
    {
        lock (_sync)
        {
            UrlToBrowserTab.Remove(url);
        }
    }

    private void OnFocusChanged(object sender, AutomationFocusChangedEventArgs e)
    {
        lock (_sync)
        {
            if (_expectedUrls.Count == 0)
                return;
        }

        try
        {
            if (sender is not AutomationElement element)
                return;

            int pid = 0;
            try
            {
                pid = element.Current.ProcessId;
            }
            catch (ElementNotAvailableException)
            {
                return;
            }

            using var process = Process.GetProcessById(pid);

            var chromium = chromiumProcessNames.Contains(process.ProcessName);
            var firefox = firefoxProcessNames.Contains(process.ProcessName);
            if (!chromium && !firefox)
                return; // not a browser

            Context.API.LogDebug(ClassName, $"The active browser is {process.ProcessName}");

            var rootElement = AutomationElement.FromHandle(process.MainWindowHandle);
            if (rootElement == null)
                return;

            Context.API.LogDebug(ClassName, $"The root element is {rootElement.Current.Name}");

            string? urlToBind;
            lock (_sync)
            {
                if (_expectedUrls.Count == 0)
                    return;

                urlToBind = _expectedUrls.Dequeue();
            }
            if (urlToBind is null)
                return;

            Context.API.LogDebug(ClassName, $"Searching for... {urlToBind}");

            // Further handling requires waiting for tabs so its better to run it on a separate thread
            _focusHandlerDispatcher?.Enqueue(urlToBind, rootElement, pid);
        }
        catch (Exception ex)
        {
            Context.API.LogException(ClassName, "Exception", ex);
        }
    }

    private void OnStructureChanged(object sender, StructureChangedEventArgs e)
    {
        //if (e.StructureChangeType == StructureChangeType.ChildAdded || e.StructureChangeType == StructureChangeType.ChildrenBulkAdded)
        //{
        //}
        var eventRuntimeId = TabsCache.RuntimeIdToKey(e.GetRuntimeId());
        switch (e.StructureChangeType)
        {
            // TODO: Consider ChildAdded to handle new tabs appearance instead of AutomationFocusChangedEventHandler
            // However think twice if it is worthwhile as the current approach based on focus might already be a good one
            //case StructureChangeType.ChildAdded:
            //    Context.API.LogDebug(ClassName, $"StructureChangeType.ChildAdded occurred on {eventRuntimeId}");
            //    break;
            //case StructureChangeType.ChildrenBulkAdded:
            //    Context.API.LogDebug(ClassName, $"StructureChangeType.ChildrenBulkAdded occurred on {eventRuntimeId}");
            //    break;
            //case StructureChangeType.ChildrenInvalidated:
            //    _walker.CheckTabExistence(eventRuntimeId, "StructureChangeType.ChildrenInvalidated");
            //    break;
            //case StructureChangeType.ChildrenReordered:
            //    _walker.CheckTabExistence(eventRuntimeId, "StructureChangeType.ChildrenReordered");
            //    break;
            case StructureChangeType.ChildRemoved:
            case StructureChangeType.ChildrenBulkRemoved:
                foreach (var window in _browserWindowsTracked)
                {
                    var windowRuntimeId = TabsCache.RuntimeIdToKey(window);
                    if (windowRuntimeId != null && eventRuntimeId.StartsWith(windowRuntimeId))
                    {
                        _walker.RescanTabsForContainer(window);
                    }
                }
                break;
        }
    }

    private void OnWindowClosed(object sender, AutomationEventArgs e)
    {
        var element = sender as AutomationElement;
        if (element == null)
            return;
        UnsubscribeStructureChangedForWindow(element);
    }

    private void UnsubscribeStructureChangedForWindow(AutomationElement wnd)
    {
        if (_browserWindowsTracked.Contains(wnd))
        {
            Context.API.LogDebug(ClassName, "Unsubscribe window from StructureChanged events");
            Automation.RemoveStructureChangedEventHandler(wnd, OnStructureChanged);
            _browserWindowsTracked.Remove(wnd);
            _walker?.RemoveAllTabs(wnd);
        }
    }

    internal void RegisterTab(string url, AutomationElement rootElement, BrowserTab currentTab)
    {
        lock (_sync)
        {
            Context.API.LogDebug(ClassName, $"Registering {url} as tab: {currentTab.Title}");
            UrlToBrowserTab[url] = currentTab;

            // required to take the tab into account by Flow Launcher main UI search window
            Context.API.ReQuery();
        }

        Automation.AddStructureChangedEventHandler(rootElement, TreeScope.Subtree, OnStructureChanged);
        Automation.AddAutomationEventHandler(WindowPattern.WindowClosedEvent, rootElement, TreeScope.Subtree, OnWindowClosed);
        _browserWindowsTracked.Add(rootElement);
    }
}
