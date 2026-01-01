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
        this.OriginQuery = new Query { RawQuery = Query };
    }

    public LastOpenedHistoryResult(Result result)
    {
        Title = result.Title;
        SubTitle = result.SubTitle;
        PluginID = result.PluginID;
        Query = result.OriginQuery.RawQuery;
        OriginQuery = result.OriginQuery;
        RecordKey = result.RecordKey;
        IcoPath = result.IcoPath;
        PluginDirectory = result.PluginDirectory;
        Glyph = result.Glyph;
        ExecutedDateTime = DateTime.Now;
        // Used for Query History style reopening
        Action = _ =>
        {
            App.API.BackToQueryResults();
            App.API.ChangeQuery(result.OriginQuery.RawQuery);
            return false;
        };
        //Used for last history style reopening, currently need to be assigned at MainViewModel.cs
        AsyncAction = null;
    }

    public LastOpenedHistoryResult Copy()
    {
        
        return new LastOpenedHistoryResult
        {
            Title = this.Title,
            SubTitle = this.SubTitle,
            PluginID = this.PluginID,
            Query = this.Query,
            OriginQuery = this.OriginQuery,
            RecordKey = this.RecordKey,
            IcoPath = this.IcoPath,
            PluginDirectory = this.PluginDirectory,
            Action = this.Action,
            AsyncAction = this.AsyncAction,
            Glyph = this.Glyph,
            ExecutedDateTime = this.ExecutedDateTime
        };
    }

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
