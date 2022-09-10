using System.Collections.Generic;
using System.Threading;

namespace Flow.Launcher.Plugin.Explorer.Search.IProvider
{
    public interface IPathIndexProvider
    {
        public IAsyncEnumerable<SearchResult> EnumerateAsync(string path, string search, bool recursive, CancellationToken token);
    }
}
