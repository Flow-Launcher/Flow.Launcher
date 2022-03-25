using Flow.Launcher.Plugin.Explorer.Search.WindowsIndex;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    public class EverythingSearchManager : IIndexProvider
    {
        private Settings Settings { get; }
        
        public EverythingSearchManager(Settings settings)
        {
            Settings = settings;
        }


        public ValueTask<IEnumerable<SearchResult>> SearchAsync(Query query, CancellationToken token)
        {
            return ValueTask.FromResult(EverythingApi.SearchAsync(query.Search, token, Settings.SortOption));
        }
        public ValueTask<IEnumerable<SearchResult>> ContentSearchAsync(Query query, CancellationToken token)
        {
            return new ValueTask<IEnumerable<SearchResult>>(new List<SearchResult>());
        }
    }
}