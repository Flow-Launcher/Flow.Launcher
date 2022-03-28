using Flow.Launcher.Plugin.Explorer.Search.WindowsIndex;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    public class EverythingSearchManager : IIndexProvider, IContentIndexProvider, IPathEnumerable
    {
        private Settings Settings { get; }
        
        public EverythingSearchManager(Settings settings)
        {
            Settings = settings;
        }


        public ValueTask<IEnumerable<SearchResult>> SearchAsync(string search, CancellationToken token)
        {
            return ValueTask.FromResult(EverythingApi.SearchAsync(search, token,  Settings.SortOption));
        }
        public ValueTask<IEnumerable<SearchResult>> ContentSearchAsync(string search, CancellationToken token)
        {
            return new ValueTask<IEnumerable<SearchResult>>(new List<SearchResult>());
        }
        public ValueTask<IEnumerable<SearchResult>> EnumerateAsync(string path, string search, bool recursive, CancellationToken token)
        {
            return new ValueTask<IEnumerable<SearchResult>>(
                EverythingApi.SearchAsync("", token, Settings.SortOption, path, recursive));
        }
    }
}