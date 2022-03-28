using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public interface IPathEnumerable
    {
        public ValueTask<IEnumerable<SearchResult>> EnumerateAsync(string path, string search, bool recursive, CancellationToken token);
    }
}