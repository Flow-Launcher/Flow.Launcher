using System.Collections.Generic;
using System.Threading;

namespace Flow.Launcher.Plugin.Explorer.Search.IProvider
{
    /// <summary>
    /// Provides functionality for searching indexed items.
    /// </summary>
    public interface IIndexProvider
    {
        /// <summary>
        /// Asynchronously searches for items matching the specified search criteria.
        /// </summary>
        /// <param name="search">The search query string.</param>
        /// <param name="token">The cancellation token to cancel the search operation.</param>
        /// <param name="allowedResultTypes">Optional collection of result types to filter the search results. If null, all result types are included.</param>
        /// <returns>An asynchronous enumerable of <see cref="SearchResult"/> objects matching the search criteria.</returns>
        public IAsyncEnumerable<SearchResult> SearchAsync(string search, CancellationToken token, IEnumerable<ResultType> allowedResultTypes = null);
    }
}
