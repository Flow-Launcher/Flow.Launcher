using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Asynchronous Dialog Jump Model
    /// </summary>
    public interface IAsyncDialogJump : IFeatures
    {
        /// <summary>
        /// Asynchronous querying for dialog jump window
        /// </summary>
        /// <para>
        /// If the Querying method requires high IO transmission
        /// or performing CPU intense jobs (performing better with cancellation), please use this IAsyncDialogJump interface
        /// </para>
        /// <param name="query">Query to search</param>
        /// <param name="token">Cancel when querying job is obsolete</param>
        /// <returns></returns>
        Task<List<DialogJumpResult>> QueryDialogJumpAsync(Query query, CancellationToken token);
    }
}
