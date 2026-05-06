using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.Calculator.Storage;

public class History
{
    [JsonInclude]
    public List<HistoryItem> Items { get; set; } = [];

    private const int MaxItems = 5;

    public void AddOrUpdate(Result result, string expression, Func<ActionContext, bool> action)
    {
        var currentItem = Items.FirstOrDefault(x => x.Query == expression);
        if (currentItem != null)
        {
            currentItem.CalculatedAt = DateTime.Now;
            return;
        }

        var item = new HistoryItem(result, expression, action);

        if (Items.Count >= MaxItems)
        {
            Items.RemoveAt(0);
        }

        Items.Add(item);
    }

}
