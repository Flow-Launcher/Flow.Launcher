using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage;
public class HistoryItem
{
    public string Title { get; set; } = string.Empty;
    public string SubTitle { get; set; } = string.Empty;
    public string IcoPath { get; set; } = string.Empty;
    public string PluginID { get; set; } = string.Empty;
    public Query OriginQuery { get; set; } = null!;
    public DateTime ExecutedDateTime { get; set; }

    public string GetTimeAgo()
    {
        return DateTimeAgo(ExecutedDateTime);
    }

    private string DateTimeAgo(DateTime dt)
    {
        var span = DateTime.Now - dt;
        if (span.Days > 365)
        {
            int years = (span.Days / 365);
            if (span.Days % 365 != 0)
                years += 1;
            return $"about {years} {(years == 1 ? "year" : "years")} ago";
        }
        if (span.Days > 30)
        {
            int months = (span.Days / 30);
            if (span.Days % 31 != 0)
                months += 1;
            return $"about {months} {(months == 1 ? "month" : "months")} ago";
        }
        if (span.Days > 0)
            return $"about {span.Days} {(span.Days == 1 ? "day" : "days")} ago";
        if (span.Hours > 0)
            return $"about {span.Hours} {(span.Hours == 1 ? "hour" : "hours")} ago";
        if (span.Minutes > 0)
            return $"about {span.Minutes} {(span.Minutes == 1 ? "minute" : "minutes")} ago";
        if (span.Seconds > 5)
            return $"about {span.Seconds} seconds ago";
        if (span.Seconds <= 5)
            return "just now";
        return string.Empty;
    }
}
