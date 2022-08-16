using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public interface IContentIndexProvider
    {
        public IAsyncEnumerable<SearchResult> ContentSearchAsync(string plainSearch, string contentSearch, CancellationToken token = default);
    }
}