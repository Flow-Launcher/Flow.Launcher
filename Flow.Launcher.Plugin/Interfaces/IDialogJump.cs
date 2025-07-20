using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Synchronous Dialog Jump Model
    /// <para>
    /// If the Querying method requires high IO transmission
    /// or performing CPU intense jobs (performing better with cancellation), please try the IAsyncDialogJump interface
    /// </para>
    /// </summary>
    public interface IDialogJump : IAsyncDialogJump
    {
        /// <summary>
        /// Querying for Dialog Jump window
        /// <para>
        /// This method will be called within a Task.Run,
        /// so please avoid synchrously wait for long.
        /// </para>
        /// </summary>
        /// <param name="query">Query to search</param>
        /// <returns></returns>
        List<DialogJumpResult> QueryDialogJump(Query query);

        Task<List<DialogJumpResult>> IAsyncDialogJump.QueryDialogJumpAsync(Query query, CancellationToken token) => Task.Run(() => QueryDialogJump(query));
    }
}
