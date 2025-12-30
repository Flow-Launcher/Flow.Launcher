using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Windows.Automation;
using Flow.Launcher.Plugin.BrowserBookmark.Tabs;
using static Flow.Launcher.Plugin.BrowserBookmark.Main;

internal sealed class TabsFocusEventDispatcher : IDisposable
{
    private static readonly string ClassName = nameof(TabsFocusEventDispatcher);
    private readonly BlockingCollection<Tuple<string, AutomationElement, int>> _queue = [];
    private readonly Thread _worker;
    private readonly CancellationTokenSource _cts = new();
    private readonly TabsWalker _walker;
    private readonly TabsTracker _tracker;

    public TabsFocusEventDispatcher(TabsWalker walker, TabsTracker tracker)
    {
        _worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "FocusEventWorker"
        };
        _worker.Start();
        _walker = walker;
        _tracker = tracker;
    }

    public void Enqueue(string url, AutomationElement element, int processId)
    {
        if (!_queue.IsAddingCompleted)
            _queue.Add(Tuple.Create(url, element, processId));
    }

    void WorkerLoop()
    {
        try
        {
            foreach (var element in _queue.GetConsumingEnumerable(_cts.Token))
            {
                HandleFocus(element);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    void HandleFocus(Tuple<string, AutomationElement, int> tuple)
    {
        var url = tuple.Item1;
        var rootElement = tuple.Item2;
        var processId = tuple.Item3;

        try
        {
            using var process = Process.GetProcessById(processId);

            var currentTab = _walker.GetCurrentTabFromWindow(rootElement, process, CancellationToken.None);
            if (currentTab != null)
            {
                _tracker.RegisterTab(url, rootElement, currentTab);
            }
        }
        catch (ArgumentException)
        {
            Context.API.LogError(ClassName, $"Process {processId} no longer runs");
        }
        catch (Exception ex)
        {
            Context.API.LogException(ClassName, "Exception", ex);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _queue.CompleteAdding();
        _worker.Join();
        _cts.Dispose();
        _queue.Dispose();
    }
}
