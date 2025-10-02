using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage;
public class ExecutedHistory
{
    [JsonInclude]
    public List<Result> Items { get; private set; } = new List<Result>();
    private int _maxHistory = 300;

    public void Add(Result result)
    {
        Items.Add(result);
    }

}
