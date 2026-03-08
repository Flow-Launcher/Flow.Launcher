using System;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage
{
    public class PinnedResultItem : Result
    {
        public DateTime AddAt { get; set; }

        public PinnedResultItem(Result result)
        {
            Title = result.Title;
            SubTitle = result.SubTitle;
            PluginID = result.PluginID;
            //Query = result.OriginQuery.TrimmedQuery;
            OriginQuery = result.OriginQuery;
            RecordKey = result.RecordKey;
            IcoPath = result.IcoPath;
            PluginDirectory = result.PluginDirectory;
            Glyph = result.Glyph;
            AddAt = DateTime.Now;
            // Used for Query History style reopening
            //Action = _ =>
            //{
            //    App.API.BackToQueryResults();
            //    App.API.ChangeQuery(result.OriginQuery.TrimmedQuery);
            //    return false;
            //};
            // Used for Last Opened History style reopening, currently need to be assigned at MainViewModel.cs
            AsyncAction = null;
        }
    }
}
