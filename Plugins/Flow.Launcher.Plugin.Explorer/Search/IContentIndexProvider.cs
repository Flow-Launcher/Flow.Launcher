using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public interface IContentIndexProvider
    {
        public ValueTask<IEnumerable<SearchResult>> ContentSearchAsync(string plainSearch, string contentSearch, CancellationToken token);
    }
}