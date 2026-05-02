using System;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.Calculator.Storage;

internal class History
{
    public List<HistoryItem> Items { get; set; } = [];

    public void AddOrUpdate(Result result, Func<ActionContext, bool> action)
    {
        var item = new HistoryItem(result, action);
        Items.Add(item);
    }
}
