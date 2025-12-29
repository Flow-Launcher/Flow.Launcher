using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using BrowserTabs;
using static Flow.Launcher.Plugin.BrowserBookmark.Main;

namespace Flow.Launcher.Plugin.BrowserBookmark.Tabs;

/// <summary>
/// TabsWalker waits for a new browser's tab to appear.
/// It uses TabsCache to keep known tabs.
/// Note that browsers don't provide full control over this process, so we have to rely on heuristics and a "best effort" approach.
/// </summary>
internal class TabsWalker
{
    private static readonly string ClassName = nameof(TabsWalker);
    private readonly TimeSpan _tabRetryTimeout = TimeSpan.FromSeconds(4);
    private readonly TimeSpan _tabRetryInterval = TimeSpan.FromMilliseconds(250);
    private readonly TabsCache _cache = new();

    private static IEnumerable<AutomationElement> FindAllValidTabs(AutomationElement mainWindow)
    {
        Condition tabCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem);
        foreach (AutomationElement tab in mainWindow.FindAll(TreeScope.Descendants, tabCondition))
        {
            var name = tab.Current.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            // on Chrome, there are kind of technical tabs that should be ignored
            var className = tab.Current.ClassName;
            if (className.Contains("bolt-tab", StringComparison.OrdinalIgnoreCase))
            {
                Context.API.LogDebug(ClassName, $"Skipping name='{name}', className='{className}'");
                continue;
            }

            yield return tab;
        }
    }

    private static BrowserTab InitiateTab(Process process, AutomationElement tab) => new()
    {
        Title = tab.Current.Name,
        BrowserName = process.ProcessName,
        Hwnd = process.MainWindowHandle,
        AutomationElement = tab
    };

    public BrowserTab GetCurrentTabFromWindow(AutomationElement mainWindow, Process process, CancellationToken cancellationToken)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var count = 1;

            while (sw.Elapsed < _tabRetryTimeout && !cancellationToken.IsCancellationRequested)
            {
                Context.API.LogDebug(ClassName, $"Start searching for a new tab... Try no {count++}");

                var tabs = FindAllValidTabs(mainWindow).ToList();
                if (tabs.Count == 0)
                {
                    Context.API.LogDebug(ClassName, "No valid tabs found");
                    Task.Delay(_tabRetryInterval, cancellationToken);
                    continue;
                }

                if (_cache.Empty())
                {
                    Context.API.LogDebug(ClassName, "First time filling the cache");
                    _cache.Add(tabs);

                    // Let's take the last one and assume this is the one that was created recently
                    // This is the best known approach as of today
                    // There might be some browsers' settings that change this behavior but weren't tested nor considered yet
                    // TODO: Research browsers' settings and check if it may break current assumption of just taking the last tab
                    return InitiateTab(process, tabs.Last());
                }

                Context.API.LogDebug(ClassName, $"Found tabs: {tabs.Count}");
                //TabsDebug.DumpElements(mainWindow, null, "Tab");

                // searching from the end and looking for a tab not in the cache
                for (var i = tabs.Count - 1; i >= 0; i--)
                {
                    var tab = tabs[i];
                    if (!_cache.Contains(tab))
                    {
                        Context.API.LogDebug(ClassName, $"FOUND A NEW TAB: name={tab.Current.Name}, className={tab.Current.ClassName}");
                        _cache.Add(tab);
                        return InitiateTab(process, tab);
                    }
                }

                Context.API.LogDebug(ClassName, "No new tab found");
                Task.Delay(_tabRetryInterval, cancellationToken);
            }

            Context.API.LogDebug(ClassName, "Timeout waiting for new tab");
        }
        catch (ElementNotAvailableException ex)
        {
            Context.API.LogException(ClassName, "Element not available", ex);
        }
        catch (Exception ex)
        {
            Context.API.LogException(ClassName, "Error getting current tab from window", ex);
        }
        return null;
    }

    internal void RescanTabsForContainer(AutomationElement browserWindow)
    {
        Context.API.LogDebug(ClassName, "Rescaning tabs in order to find removed tabs");
        _cache.RemoveAllNonExistentTabs(browserWindow, FindAllValidTabs(browserWindow));
    }

    internal void RemoveAllTabs(AutomationElement browserWindow)
    {
        Context.API.LogDebug(ClassName, "Removing all tabs in a window");
        _cache.RemoveAllNonExistentTabs(browserWindow, []);
    }
}
