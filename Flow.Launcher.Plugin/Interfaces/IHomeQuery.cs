using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Synchronous Query Model for Flow Launcher When Query Text is Empty
    /// <para>
    /// If the Querying method requires high IO transmission
    /// or performing CPU intense jobs (performing better with cancellation), please try the IAsyncHomeQuery interface
    /// </para>
    /// </summary>
    public interface IHomeQuery : IAsyncHomeQuery
    {
        /// <summary>
        /// Querying When Query Text is Empty
        /// <para>
        /// This method will be called within a Task.Run,
        /// so please avoid synchronously wait for long.
        /// </para>
        /// </summary>
        /// <returns></returns>
        List<Result> HomeQuery();

        Task<List<Result>> IAsyncHomeQuery.HomeQueryAsync(CancellationToken token) => Task.Run(HomeQuery);
    }
}
