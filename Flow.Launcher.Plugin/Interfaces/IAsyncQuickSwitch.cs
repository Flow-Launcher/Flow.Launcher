using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Asynchronous Quick Switch Model
    /// </summary>
    public interface IAsyncQuickSwitch : IFeatures
    {
        /// <summary>
        /// Asynchronous querying for quick switch window
        /// </summary>
        /// <para>
        /// If the Querying method requires high IO transmission
        /// or performing CPU intense jobs (performing better with cancellation), please use this IAsyncQuickSwitch interface
        /// </para>
        /// <param name="query">Query to search</param>
        /// <param name="token">Cancel when querying job is obsolete</param>
        /// <returns></returns>
        Task<List<QuickSwitchResult>> QueryQuickSwitchAsync(Query query, CancellationToken token);
    }
}
