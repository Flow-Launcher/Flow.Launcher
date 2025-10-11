using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Flow.Launcher.Storage;
public class HistoryLegacy
{
    [JsonInclude] public List<HistoryItemLegacy> Items { get; private set; } = [];
}
