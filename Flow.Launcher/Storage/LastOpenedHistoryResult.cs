using System;
using System.DirectoryServices.ActiveDirectory;
using Flow.Launcher.Helper;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage;

public class LastOpenedHistoryResult : Result
{
    public string Query { get; set; } = string.Empty;
    
    public DateTime ExecutedDateTime { get; set; }

    public LastOpenedHistoryResult()
    {
    }

    public LastOpenedHistoryResult(Result result)
    {
        Title = result.Title;
        SubTitle = result.SubTitle;
        PluginID = result.PluginID;
        Query = result.OriginQuery.RawQuery;
        RecordKey = result.RecordKey;
        IcoPath = result.IcoPath;
        PluginDirectory = result.PluginDirectory;
        Glyph = result.Glyph;
        ExecutedDateTime = DateTime.Now;
    }

    //public Result ToResult(bool isQueryHistoryStyle)
    //{
    //    Result result = null;
        
    //    if (isQueryHistoryStyle)
    //    {
    //        result = new Result
    //        {
    //            Action = _ =>
    //            {
    //                App.API.BackToQueryResults();
    //                App.API.ChangeQuery(Query);
    //                return false;
    //            },
    //            Glyph = Glyph,
    //        };
    //    }
    //    else
    //    {
    //        result = new Result
    //        {
    //            AsyncAction = async c =>
    //            {
    //                var reflectResult = await ResultHelper.PopulateResultsAsync(item);
    //                if (reflectResult != null)
    //                {
    //                    // Record the user selected record for result ranking
    //                    _userSelectedRecord.Add(reflectResult);

    //                    // Since some actions may need to hide the Flow window to execute
    //                    // So let us populate the results of them
    //                    return await reflectResult.ExecuteAsync(c);
    //                }

    //                // If we cannot get the result, fallback to re-query
    //                App.API.BackToQueryResults();
    //                App.API.ChangeQuery(item.Query);
    //                return false;
    //            },
    //            Glyph = Glyph,
    //        };
    //    }

    //    var result = new Result
    //    {
    //        Title = Title,
    //        SubTitle = Localize.lastExecuteTime(ExecutedDateTime),
    //        IcoPath = IcoPath,
    //        OriginQuery = new Query { RawQuery = Query },
    //        Action = _ =>
    //        {
    //            App.API.BackToQueryResults();
    //            App.API.ChangeQuery(Query);
    //            return false;
    //        },
    //        Glyph = Glyph,
    //    };
    //}

    public bool Equals(Result r)
    {
        if (string.IsNullOrEmpty(RecordKey) || string.IsNullOrEmpty(r.RecordKey))
        {
            return Title == r.Title
                && SubTitle == r.SubTitle
                && PluginID == r.PluginID
                && Query == r.OriginQuery.RawQuery;
        }
        else
        {
            return RecordKey == r.RecordKey
                && PluginID == r.PluginID
                && Query == r.OriginQuery.RawQuery;
        }
    }
}
