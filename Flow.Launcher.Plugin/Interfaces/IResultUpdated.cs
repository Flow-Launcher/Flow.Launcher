using System;
using System.Collections.Generic;
using System.Threading;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Interface for plugins that want to manually update their results
    /// </summary>
    public interface IResultUpdated : IFeatures
    {
        /// <summary>
        /// Event that is triggered when the results are updated
        /// </summary>
        event ResultUpdatedEventHandler ResultsUpdated;
    }

    /// <summary>
    /// Delegate for the ResultsUpdated event
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public delegate void ResultUpdatedEventHandler(IResultUpdated sender, ResultUpdatedEventArgs e);

    /// <summary>
    /// Event arguments for the ResultsUpdated event
    /// </summary>
    public class ResultUpdatedEventArgs : EventArgs
    {
        /// <summary>
        /// List of results that should be displayed
        /// </summary>
        public List<Result> Results;

        /// <summary>
        /// Query that triggered the update
        /// </summary>
        public Query Query;

        /// <summary>
        /// Token that can be used to cancel the update
        /// </summary>
        public CancellationToken Token { get; init; }
    }
}
