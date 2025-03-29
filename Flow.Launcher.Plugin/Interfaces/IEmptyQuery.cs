using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Synchronous Query Model for Flow Launcher When Query Text is Empty
    /// <para>
    /// If the Querying method requires high IO transmission
    /// or performaing CPU intense jobs (performing better with cancellation), please try the IAsyncEmptyQuery interface
    /// </para>
    /// </summary>
    public interface IEmptyQuery : IAsyncEmptyQuery
    {
        /// <summary>
        /// Querying When Query Text is Empty
        /// <para>
        /// This method will be called within a Task.Run,
        /// so please avoid synchrously wait for long.
        /// </para>
        /// </summary>
        /// <returns></returns>
        List<Result> EmptyQuery();

        Task<List<Result>> IAsyncEmptyQuery.EmptyQueryAsync(CancellationToken token) => Task.Run(EmptyQuery);
    }
}
