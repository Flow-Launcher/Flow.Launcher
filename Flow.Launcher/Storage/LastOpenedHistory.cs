using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.Storage;
public class LastOpenedHistory
{
    [JsonInclude] public List<LastOpenedHistoryItem> Items { get; private set; } = [];
    private const int MaxHistory = 300;

    public void Add(Result result)
    {
        var item = new LastOpenedHistoryItem
        {
            Title = result.Title,
            SubTitle = result.SubTitle,
            IcoPath = result.IcoPath ?? string.Empty,
            PluginID = result.PluginID,
            OriginQuery = result.OriginQuery,
            ExecutedDateTime = DateTime.Now
        };

        var existing = Items.FirstOrDefault(x => x.OriginQuery.RawQuery == item.OriginQuery.RawQuery && x.PluginID == item.PluginID);
        if (existing != null)
        {
            Items.Remove(existing);
        }

        Items.Add(item);

        if (Items.Count > MaxHistory)
        {
            Items.RemoveAt(0);
        }
    }
}
