using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Automation;
using static Flow.Launcher.Plugin.BrowserBookmark.Main;
using static Flow.Launcher.Plugin.BrowserBookmark.Tabs.TabsReservationService;

namespace Flow.Launcher.Plugin.BrowserBookmark.Tabs;

/// <summary>
/// TabsTracker builds full list of all browsers windows and their tabs.
/// TabsTracker also maintains the lists (invalidates on events, updates lazily on demand)
/// </summary>
public class TabsTracker : IDisposable
{
    private static readonly string ClassName = nameof(TabsTracker);

    // Firefox - tested on version: 146.0.1
    // Chrome  - tested on version: 143.0.7499.170
    // Edge    - tested on version: 143.0.3650.96
    // Brave   - NOT tested
    // Vivaldi - NOT tested
    // Opera   - NOT tested
    private static readonly HashSet<string> firefoxProcessNames = new(["firefox"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> chromiumProcessNames = new(["msedge", "chrome", "brave", "vivaldi", "opera", "chromium"], StringComparer.OrdinalIgnoreCase);

    private bool _trackingEnabled = false;

    private readonly Lock _sync = new();
    private int _lastAssignedIndex;
    private bool _windowsHandlerInitialized = false;
    private Dictionary<AutomationElement, TrackingInfo> _browserWindowsTracked = [];

    private readonly ConcurrentQueue<Tuple<string, TokenForNewTab>> _expectedUrls = [];

    private TabsEventsDispatcher _eventsDispatcher;

    private readonly Lock _syncInvalidations = new();
    private HashSet<AutomationElement> _structureInvalidations = [];

    private TabsReservationService _service;

    public TabsTracker(TabsReservationService service)
    {
        _service = service;      
    }

    public static string RuntimeIdToKey(AutomationElement elem)
    {
        try
        {
            return elem != null ? string.Join("-", elem.GetRuntimeId()) : null;
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
    }

    private static IEnumerable<AutomationElement> FindAllValidTabs(AutomationElement browserWindow)
    {
        Condition tabCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem);

        foreach (AutomationElement tab in browserWindow.FindAll(TreeScope.Descendants, tabCondition))
        {
            var name = tab.Current.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            // There are kind of technical tabs that should be ignored
            var className = tab.Current.ClassName;
            if (className.Contains("bolt-tab", StringComparison.OrdinalIgnoreCase))
            {
                Context.API.LogDebug(ClassName, $"TABS:Skipping name='{name}', className='{className}'");
                continue;
            }

            yield return tab;
        }
    }

    /// <summary>
    /// TrackingInfo keeps context of a single browser window
    /// </summary>
    public class TrackingInfo(AutomationElement rootElement, StructureChangedEventHandler structureChangedHandler, AutomationEventHandler windowCloseHandler, string processName, nint processMainWindowHandle)
    {
        public AutomationElement RootElement { get; init; } = rootElement;
        public StructureChangedEventHandler StructureChangedHandler { get; init; } = structureChangedHandler;
        public AutomationEventHandler WindowCloseHandler { get; init; } = windowCloseHandler;
        public string ProcessName { get; init; } = processName;
        public nint ProcessMainWindowHandle { get; init; } = processMainWindowHandle;
        public TabsCache Cache { get; init; } = new TabsCache();
    }

    public void Dispose()
    {
        DisableTracking();
    }

    /// <summary>
    /// Makes snapshot of all browsers windows and all their tabs.
    /// Optionally it may register a token.
    /// </summary>
    public void MakeSnapshot(Func<int, TabsReservationService.TokenForNewTab> registerToken = null, string requestedUrl = null)
    {
        lock (_sync)
        {
            EnsureHavingAllBrowsersWindows();
            EnsureHavingAllBrowsersTabs();
            if (registerToken != null)
            {
                var token = registerToken(_lastAssignedIndex);
                _expectedUrls.Enqueue(Tuple.Create(requestedUrl, token));
            }
        }
    }

    private void EnsureHavingAllBrowsersWindows()
    {
        // this is done once
        // later on list is updated using WindowOpen / WindowClose events
        if (!_windowsHandlerInitialized)
        {
            Context.API.LogDebug(ClassName, "TABS:EnsureHavingAllBrowsersWindows initializing ...");

            var desktop = AutomationElement.RootElement;
            Automation.AddAutomationEventHandler(WindowPattern.WindowOpenedEvent, desktop, TreeScope.Children, OnWindowOpen);

            var topLevelWindows = desktop.FindAll(TreeScope.Children, Condition.TrueCondition);
            foreach (AutomationElement element in topLevelWindows)
            {
                HandleProcessStart(element);
            }

            _windowsHandlerInitialized = true;
        }
    }

    private void OnWindowOpen(object src, AutomationEventArgs e)
    {
        Context.API.LogDebug(ClassName, "TABS:OnWindowOpen");
        AutomationElement element = src as AutomationElement;
        if (element != null)
            HandleProcessStart(element);
    }

    private void OnWindowClose(AutomationElement element, object src, AutomationEventArgs e)
    {
        Context.API.LogDebug(ClassName, $"TABS:OnWindowClose {RuntimeIdToKey(element)}");
        lock (_sync)
        {
            bool structureChangesTracked = _browserWindowsTracked.TryGetValue(element, out var trackingInfo);
            if (structureChangesTracked && trackingInfo.WindowCloseHandler != null)
            {
                Automation.RemoveAutomationEventHandler(WindowPattern.WindowClosedEvent, element, trackingInfo.WindowCloseHandler);
            }

            HandleProcessExit(element);
        }
    }

    private static Process TryProcess(int processId)
    {
        try
        {
            return Process.GetProcessById(processId);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void HandleProcessStart(AutomationElement element)
    {
        var processId = element.Current.ProcessId;
        using var process = TryProcess(processId);
        if (process == null)
            return;

        string processName = process.ProcessName.ToLowerInvariant();
        var chromium = chromiumProcessNames.Contains(processName);
        var firefox = firefoxProcessNames.Contains(processName);
        if (!chromium && !firefox)
            return;

        Context.API.LogDebug(ClassName, $"TABS:Found a browser window for {processName}, PID={processId}");
        lock (_sync)
        {
            bool structureChangesTracked = _browserWindowsTracked.ContainsKey(element);
            if (!structureChangesTracked)
            {
                SubscribeStructureChangedEventHandler(element, process);
            }
        }
    }

    private void HandleProcessExit(AutomationElement element)
    {
        lock (_sync)
        {
            bool structureChangesTracked = _browserWindowsTracked.TryGetValue(element, out var trackingInfo);
            if (structureChangesTracked)
            {
                UnsubscribeStructureChangedEventHandler(element, trackingInfo);
            }
        }
    }

    private void SubscribeStructureChangedEventHandler(AutomationElement element, Process process)
    {
        void structureChangedHandler(object sender, StructureChangedEventArgs e)
        {
            OnStructureChanged(element, sender, e);
        }

        Automation.AddStructureChangedEventHandler(element, TreeScope.Subtree, structureChangedHandler);

        void windowCloseHandler(object src, AutomationEventArgs e)
        {
            OnWindowClose(element, src, e);
        }

        _browserWindowsTracked[element] = new TrackingInfo(element, structureChangedHandler, windowCloseHandler, process.ProcessName, process.MainWindowHandle);

        Automation.AddAutomationEventHandler(WindowPattern.WindowClosedEvent, element, TreeScope.Element, windowCloseHandler);

        Context.API.LogDebug(ClassName, $"TABS:Window {RuntimeIdToKey(element)} SUBSCRIBED for StructureChanged events");
    }

    private void UnsubscribeStructureChangedEventHandler(AutomationElement element, TrackingInfo trackingInfo)
    {
        Automation.RemoveStructureChangedEventHandler(element, trackingInfo.StructureChangedHandler);
        _service.UnregisterTabs(trackingInfo.Cache.GetTabs());
        _browserWindowsTracked.Remove(element);

        Context.API.LogDebug(ClassName, $"TABS:Window {RuntimeIdToKey(element)} UNSUBSCRIBED from StructureChanged events");
    }

    private void EnsureHavingAllBrowsersTabs()
    {
        Context.API.LogDebug(ClassName, "TABS:EnsureHavingAllBrowsersTabs ...");

        List<AutomationElement> elementsToInvalidate;
        lock (_syncInvalidations)
        {
            elementsToInvalidate = _structureInvalidations.Where(_browserWindowsTracked.ContainsKey).ToList();
            _structureInvalidations.Clear();
        }
        foreach (var element in elementsToInvalidate)
        {
            _browserWindowsTracked[element].Cache.Invalidate();
        }

        foreach (var pair in _browserWindowsTracked)
        {
            if (!pair.Value.Cache.Valid)
            {
                try
                {
                    _lastAssignedIndex = pair.Value.Cache.UpdateTabs(_lastAssignedIndex, [.. FindAllValidTabs(pair.Key)], out var removedTabs);
                    _service.UnregisterTabs(removedTabs);
                }
                catch (ElementNotAvailableException)
                {
                    Context.API.LogError(ClassName, "ElementNotAvailableException while updating tabs");
                }
            }
        }
    }

    private void OnStructureChanged(AutomationElement window, object sender, StructureChangedEventArgs e)
    {
        //Context.API.LogDebug(ClassName, $"TABS:Received {e.StructureChangeType.ToString()} on {sender}");
        switch (e.StructureChangeType)
        {
            case StructureChangeType.ChildAdded:
            case StructureChangeType.ChildrenBulkAdded:
            case StructureChangeType.ChildrenInvalidated:
            case StructureChangeType.ChildrenReordered:
            case StructureChangeType.ChildRemoved:
            case StructureChangeType.ChildrenBulkRemoved:
                lock (_syncInvalidations)
                {
                    _structureInvalidations.Add(window);
                }
                break;
        }
        switch (e.StructureChangeType)
        {
            case StructureChangeType.ChildAdded:
            case StructureChangeType.ChildrenBulkAdded:
                while (_expectedUrls.TryDequeue(out var tuple))
                {
                    lock (_sync)
                    {
                        _eventsDispatcher ??= new(this, _service);
                        _eventsDispatcher.Enqueue(tuple.Item1, tuple.Item2);
                    }
                }
                break;
        }
    }

    public (AutomationElement, TrackingInfo) TryGetTab(int expectedIndex)
    {
        lock (_sync)
        {
            foreach (var trackingInfo in _browserWindowsTracked.Values)
            {
                var tab = trackingInfo.Cache.TryGetTab(expectedIndex);
                if (tab != null)
                {
                    return (tab, trackingInfo);
                }
            }
            return (null, null);
        }
    }

    public void EnableTracking(bool enable)
    {
        lock (_sync)
        {
            if (_trackingEnabled == enable)
                return;

            _trackingEnabled = enable;
            if (!_trackingEnabled)
            {
                DisableTracking();
            }
        }
    }

    private void DisableTracking()
    {
        lock (_syncInvalidations)
        {
            _structureInvalidations.Clear();
        }

        lock (_sync)
        {
            _eventsDispatcher?.Dispose();
            _eventsDispatcher = null;

            if (_windowsHandlerInitialized)
            {
                Automation.RemoveAutomationEventHandler(WindowPattern.WindowOpenedEvent, AutomationElement.RootElement, OnWindowOpen);

                foreach (var tracking in _browserWindowsTracked)
                {
                    UnsubscribeStructureChangedEventHandler(tracking.Key, tracking.Value);
                }
                _browserWindowsTracked.Clear();
                _expectedUrls.Clear();

                _windowsHandlerInitialized = false;
            }
        }
    }
}
