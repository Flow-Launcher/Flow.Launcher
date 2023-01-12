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

        public CancellationToken Token { get; }

        public ResultsForUpdate(IReadOnlyList<Result> results, PluginMetadata metadata, CancellationToken token)
        {
            Results = results;
            Metadata = metadata;
            Token = token;
            ID = metadata.ID;
        }
    }
}
