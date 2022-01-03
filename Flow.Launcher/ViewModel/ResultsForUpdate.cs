using Flow.Launcher.Plugin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Flow.Launcher.ViewModel
{
    public struct ResultsForUpdate
    {
        public IReadOnlyList<Result> Results { get; }

        public PluginMetadata Metadata { get; }
        public string ID { get; }

        public Query Query { get; }
        public CancellationToken Token { get; }

        public ResultsForUpdate(IReadOnlyList<Result> results, PluginMetadata metadata, Query query, CancellationToken token)
        {
            Results = results;
            Metadata = metadata;
            Query = query;
            Token = token;
            ID = metadata.ID;
        }
    }
}
