using System;
using System.Collections.Generic;
using System.Threading;

namespace Flow.Launcher.Plugin.Explorer.Search.IProvider
{
    public interface IContentIndexProvider
    {
        public IAsyncEnumerable<SearchResult> ContentSearchAsync(ReadOnlySpan<char> plainSearch, ReadOnlySpan<char> contentSearch, CancellationToken token = default);
    }
}
