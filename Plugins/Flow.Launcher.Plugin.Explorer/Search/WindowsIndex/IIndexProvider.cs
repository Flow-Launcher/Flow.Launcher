using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Explorer.Search.WindowsIndex
{
    public interface IIndexProvider
    {
        public ValueTask<IEnumerable<SearchResult>> SearchAsync(Query query, CancellationToken token);
        public ValueTask<IEnumerable<SearchResult>> ContentSearchAsync(Query query, CancellationToken token);
    }
}