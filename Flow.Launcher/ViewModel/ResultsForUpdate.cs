using Flow.Launcher.Plugin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Flow.Launcher.ViewModel
{
    public class ResultsForUpdate
    {
        public List<Result> Results { get; }

        public PluginMetadata Metadata { get; }
        public string ID { get; }

        public Query Query { get; }
        public CancellationToken Token { get; }

        public ResultsForUpdate(List<Result> results, string resultID, CancellationToken token)
        {
            Results = results;
            ID = resultID;
            Token = token;
        }

        public ResultsForUpdate(List<Result> results, PluginMetadata metadata, Query query, CancellationToken token)
        {
            Results = results;
            Metadata = metadata;
            Query = query;
            Token = token;
            ID = metadata.ID;
        }
    }
}
