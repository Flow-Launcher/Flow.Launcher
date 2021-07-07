using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Synchronous Plugin Model for Flow Launcher
    /// <para>
    /// If the Querying or Init method requires high IO transmission
    /// or performaing CPU intense jobs (performing better with cancellation), please try the IAsyncPlugin interface
    /// </para>
    /// </summary>
    public interface IPlugin : IAsyncPlugin
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

        Task IAsyncPlugin.InitAsync(PluginInitContext context) => Task.Run(() => Init(context));

        Task<List<Result>> IAsyncPlugin.QueryAsync(Query query, CancellationToken token) => Task.Run(() => Query(query));
    }
}
