using System.Collections.Generic;
using System.Threading;

namespace Flow.Launcher.Plugin.Explorer.Search.IProvider
{
    public interface IContentIndexProvider
    {
        public IAsyncEnumerable<SearchResult> ContentSearchAsync(string plainSearch, string contentSearch, CancellationToken token = default);
    }
}
