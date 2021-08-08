using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Asynchronous Plugin Model for Flow Launcher
    /// </summary>
    public interface IAsyncPlugin
    {
        /// <summary>
        /// Asynchronous Querying
        /// </summary>
        /// <para>
        /// If the Querying or Init method requires high IO transmission
        /// or performing CPU intense jobs (performing better with cancellation), please use this IAsyncPlugin interface
        /// </para>
        /// <param name="query">Query to search</param>
        /// <param name="token">Cancel when querying job is obsolete</param>
        /// <returns></returns>
        Task<List<Result>> QueryAsync(Query query, CancellationToken token);

        /// <summary>
        /// Initialize plugin asynchrously (will still wait finish to continue)
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        Task InitAsync(PluginInitContext context);
    }
}
