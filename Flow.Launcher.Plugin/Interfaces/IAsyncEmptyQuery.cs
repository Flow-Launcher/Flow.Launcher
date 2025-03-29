using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Asynchronous Query Model for Flow Launcher When Query Text is Empty
    /// </summary>
    public interface IAsyncEmptyQuery
    {
        /// <summary>
        /// Asynchronous Querying When Query Text is Empty
        /// </summary>
        /// <para>
        /// If the Querying method requires high IO transmission
        /// or performing CPU intense jobs (performing better with cancellation), please use this IAsyncEmptyQuery interface
        /// </para>
        /// <param name="token">Cancel when querying job is obsolete</param>
        /// <returns></returns>
        Task<List<Result>> EmptyQueryAsync(CancellationToken token);
    }
}
