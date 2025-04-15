using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Synchronous Quick Switch Model
    /// <para>
    /// If the Querying method requires high IO transmission
    /// or performaing CPU intense jobs (performing better with cancellation), please try the IAsyncQuickSwitch interface
    /// </para>
    /// </summary>
    public interface IQuickSwitch : IAsyncQuickSwitch
    {
        /// <summary>
        /// Querying for quick switch window
        /// <para>
        /// This method will be called within a Task.Run,
        /// so please avoid synchrously wait for long.
        /// </para>
        /// </summary>
        /// <param name="query">Query to search</param>
        /// <returns></returns>
        List<QuickSwitchResult> QueryQuickSwitch(Query query);

        Task<List<QuickSwitchResult>> IAsyncQuickSwitch.QueryQuickSwitchAsync(Query query, CancellationToken token) => Task.Run(() => QueryQuickSwitch(query));
    }
}
