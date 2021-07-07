using System;
using System.Collections.Generic;
using System.Threading;

namespace Flow.Launcher.Plugin
{
    public interface IResultUpdated : IFeatures
    {
        event ResultUpdatedEventHandler ResultsUpdated;
    }

    public delegate void ResultUpdatedEventHandler(IResultUpdated sender, ResultUpdatedEventArgs e);

    public class ResultUpdatedEventArgs : EventArgs
    {
        public List<Result> Results;
        public Query Query;
        public CancellationToken Token { get; init; }
    }
}