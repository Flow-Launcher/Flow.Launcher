using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flow.Launcher.Storage;
public class HistoryItemLegacy
{
    public string Query { get; set; }
    public DateTime? ExecutedDateTime { get; set; }

}
