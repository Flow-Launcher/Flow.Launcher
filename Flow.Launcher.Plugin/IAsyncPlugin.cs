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