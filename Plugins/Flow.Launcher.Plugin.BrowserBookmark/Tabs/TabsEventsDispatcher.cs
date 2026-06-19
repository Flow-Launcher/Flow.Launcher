using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Flow.Launcher.Plugin.BrowserBookmark.Tabs;
using static Flow.Launcher.Plugin.BrowserBookmark.Main;

/// <summary>
/// TabsEventsDispatcher handles events in a separate thread.
/// This is to make handlers fast by not to blocking them.
/// </summary>
public sealed class TabsEventsDispatcher : IDisposable
{
    private static readonly string ClassName = nameof(TabsEventsDispatcher);

    private readonly BlockingCollection<Tuple<string, TabsReservationService.TokenForNewTab>> _queue = [];
    private readonly Thread _worker;
    private readonly CancellationTokenSource _cts = new();
    private readonly TabsReservationService _reservationService;

    private readonly TimeSpan _tabRetryTimeout = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _tabRetryInterval = TimeSpan.FromMilliseconds(250);

    public TabsEventsDispatcher(TabsTracker tracker, TabsReservationService reservationService)
    {
        _reservationService = reservationService;

        _worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "TabsEventsDispatcher"
        };
        _worker.Start();
    }

    public void Enqueue(string url, TabsReservationService.TokenForNewTab token)
    {
        if (!_queue.IsAddingCompleted)
            _queue.Add(Tuple.Create(url, token));
    }

    void WorkerLoop()
    {
        try
        {
            foreach (var tuple in _queue.GetConsumingEnumerable(_cts.Token))
            {
                HandleUrl(tuple);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    void HandleUrl(Tuple<string, TabsReservationService.TokenForNewTab> tuple)
    {
        var url = tuple.Item1;
        var tokenForNewTab = tuple.Item2;

        var sw = Stopwatch.StartNew();
        var count = 1;

        while (sw.Elapsed < _tabRetryTimeout && !_cts.Token.IsCancellationRequested)
        {
            var element = _reservationService.TryToResolveToken(tokenForNewTab, out var trackingInfo);
            if (element != null)
            {
                _reservationService.RegisterTab(url, trackingInfo, element);
                return;
            }

            Context.API.LogDebug(ClassName, $"TABS:No new tab found on try {count++}. Will sleep for {_tabRetryInterval.TotalMilliseconds} ms.");
            Thread.Sleep(_tabRetryInterval);
        }

        Context.API.LogError(ClassName, "TABS:Timeout waiting for a new tab - assuming events are guaranteed and handled well this situation should not happen");
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
