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
            return ValueTask.FromResult(EverythingApi.SearchAsync(
                new EverythingSearchOption(search, Settings.SortOption),
                token));
        }
        public ValueTask<IEnumerable<SearchResult>> ContentSearchAsync(string plainSearch,
            string contentSearch, CancellationToken token)
        {
            if (!Settings.EnableEverythingContentSearch)
            {
                return new ValueTask<IEnumerable<SearchResult>>(new List<SearchResult>());
            }

            return new ValueTask<IEnumerable<SearchResult>>(EverythingApi.SearchAsync(
                new EverythingSearchOption(
                    plainSearch,
                    Settings.SortOption,
                    true,
                    contentSearch),
                token));
        }
        public ValueTask<IEnumerable<SearchResult>> EnumerateAsync(string path, string search, bool recursive, CancellationToken token)
        {
            return new ValueTask<IEnumerable<SearchResult>>(
                EverythingApi.SearchAsync(
                    new EverythingSearchOption(search,
                        Settings.SortOption,
                        parentPath: path,
                        isRecursive: recursive),
                    token));
        }
    }
}