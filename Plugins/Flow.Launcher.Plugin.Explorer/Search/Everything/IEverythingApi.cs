using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    public interface IEverythingApi
    {
        ValueTask<bool> IsEverythingRunningAsync(CancellationToken token = default);
        IAsyncEnumerable<SearchResult> SearchAsync(EverythingSearchOption option, CancellationToken token = default);
        Task IncrementRunCounterAsync(string fileOrFolder);
        bool IsFastSortOption(EverythingSortOption sortOption);
    }
}
