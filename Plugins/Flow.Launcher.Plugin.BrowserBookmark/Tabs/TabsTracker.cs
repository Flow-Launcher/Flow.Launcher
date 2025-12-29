using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Automation;
using BrowserTabs;
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using static Flow.Launcher.Plugin.BrowserBookmark.Main;

namespace Flow.Launcher.Plugin.BrowserBookmark.Tabs;

/// <summary>
/// TabsTracker maps initial URLs into existing browser's tabs.
/// The sequence of events:
/// 1. OpenUrlAndTrack - before lauching an URL it is remembered for later mapping to a browser's tab
/// 2. OnFocusChanged - whenever a browser's window gets focused a new tab discovery is started and result is put into the UrlToBrowserTab map
/// 3. InjectExistingTabs - iterates over BrowserBookmark's query result and replaces OpenUrl with ActivateTab for known, existing tabs
/// </summary>
public class TabsTracker : IDisposable
{
    private static readonly string ClassName = nameof(TabsTracker);
    private static readonly HashSet<string> chromiumProcessNames = new HashSet<string>(["msedge", "chrome", "brave", "vivaldi", "opera", "chromium"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> firefoxProcessNames = new HashSet<string>(["firefox"], StringComparer.OrdinalIgnoreCase);
    private readonly TabsWalker _walker = new();
    private string? _expectedUrl;
    private Dictionary<string, BrowserTab> UrlToBrowserTab { get; } = [];
    private readonly object _sync = new();

    private AutomationFocusChangedEventHandler? _focusHandler;
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

        _focusHandler = OnFocusChanged;
        Automation.AddAutomationFocusChangedEventHandler(_focusHandler);
        _initialized = true;
    }

    public void Dispose()
    {
        if (_focusHandler != null)
            Automation.RemoveAutomationFocusChangedEventHandler(_focusHandler);
    }

    public void ExpectUrl(string url)
    {
        lock (_sync)
        {
            if (_expectedUrl != null)
            {
                Context.API.LogError(ClassName, $"Opening {url} while older is still not resolved ({_expectedUrl}). Forgetting the older.");
            }
            _expectedUrl = url;
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
        string? urlToBind;
        lock (_sync)
        {
            urlToBind = _expectedUrl;
        }
        if (urlToBind is null)
            return;

        try
        {
            Context.API.LogDebug(ClassName, $"Searching for... {urlToBind}");

            if (sender is not AutomationElement element)
                return;

            int pid = element.Current.ProcessId;
            try
            {
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

                var currentTab = _walker.GetCurrentTabFromWindow(rootElement, process, CancellationToken.None);
                if (currentTab != null)
                {
                    lock (_sync)
                    {
                        Context.API.LogDebug(ClassName, $"Registering {urlToBind} as tab: {currentTab.Title}");
                        UrlToBrowserTab[urlToBind] = currentTab;
                        _expectedUrl = null;

                        // required to take the tab into account by Flow Launcher main UI search window
                        Context.API.ReQuery();
                    }
                }
            }
            catch (ArgumentException)
            {
                // No such process / not running
                return;
            }
        }
        catch (Exception ex)
        {
            Context.API.LogException(ClassName, "Exception", ex);
        }
    }
}
