using System.Collections.Generic;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Synchronous Plugin Model for Flow Launcher
    /// <para>
    /// If you assume that Querying or Init method require IO transmission
    /// or CPU Intense Job (performing better with cancellation), please try IAsyncPlugin interface
    /// </para>
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Querying when user's search changes
        /// <para>
        /// This method will be called within a Task.Run,
        /// so please avoid synchrously wait for long.
        /// </para>
        /// </summary>
        /// <param name="query">Query to search</param>
        /// <returns></returns>
        List<Result> Query(Query query);

        /// <summary>
        /// Initialize plugin
        /// </summary>
        /// <param name="context"></param>
        void Init(PluginInitContext context);
    }
}