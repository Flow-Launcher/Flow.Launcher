using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public interface IIndexProvider
    {
        public ValueTask<IEnumerable<SearchResult>> SearchAsync(string search, CancellationToken token);
    }
}