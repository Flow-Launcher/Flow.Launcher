using System;
using System.Collections.Generic;
using System.Threading;

namespace Flow.Launcher.Plugin.Explorer.Search.IProvider
{
    public interface IPathIndexProvider
    {
        public IAsyncEnumerable<SearchResult> EnumerateAsync(ReadOnlySpan<char> path, ReadOnlySpan<char> search, bool recursive, CancellationToken token);
    }
}
