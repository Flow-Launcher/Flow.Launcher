using Flow.Launcher.Plugin.Explorer.Search.WindowsIndex;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Explorer.Search.IProvider;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    public class EverythingSearchManager : IIndexProvider, IContentIndexProvider, IPathIndexProvider
    {
        private Settings Settings { get; }

        public EverythingSearchManager(Settings settings)
        {
            Settings = settings;
        }


        public IAsyncEnumerable<SearchResult> SearchAsync(string search, CancellationToken token)
        {
            return EverythingApi.SearchAsync(
                new EverythingSearchOption(search, Settings.SortOption),
                token);
        }
        public IAsyncEnumerable<SearchResult> ContentSearchAsync(string plainSearch,
            string contentSearch, CancellationToken token)
        {
            if (!Settings.EnableEverythingContentSearch)
            {
                return AsyncEnumerable.Empty<SearchResult>();
            }

            return EverythingApi.SearchAsync(
                new EverythingSearchOption(
                    plainSearch,
                    Settings.SortOption,
                    true,
                    contentSearch),
                token);
        }
        public IAsyncEnumerable<SearchResult> EnumerateAsync(string path, string search, bool recursive, CancellationToken token)
        {
            return EverythingApi.SearchAsync(
                    new EverythingSearchOption(search,
                        Settings.SortOption,
                        ParentPath: path,
                        IsRecursive: recursive),
                    token);
        }
    }
}
