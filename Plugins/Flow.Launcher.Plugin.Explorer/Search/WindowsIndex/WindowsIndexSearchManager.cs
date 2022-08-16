using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Explorer.Search.WindowsIndex
{
    public class WindowsIndexSearchManager : IIndexProvider, IContentIndexProvider, IPathEnumerable
    {
        private Settings Settings { get; }
        private QueryConstructor QueryConstructor { get; }
        public WindowsIndexSearchManager(Settings settings)
        {
            Settings = settings;
            QueryConstructor = new QueryConstructor(Settings);
        }
        
        private IAsyncEnumerable<SearchResult> WindowsIndexFileContentSearchAsync(string querySearchString,
            CancellationToken token)
        {
            if (string.IsNullOrEmpty(querySearchString))
                return AsyncEnumerable.Empty<SearchResult>();

            return WindowsIndex.WindowsIndexSearchAsync(
                querySearchString,
                QueryConstructor.CreateQueryHelper,
                QueryConstructor.QueryForFileContentSearch,
                Settings.IndexSearchExcludedSubdirectoryPaths,
                token);
        }

        private IAsyncEnumerable<SearchResult> WindowsIndexFilesAndFoldersSearchAsync(string querySearchString,
            CancellationToken token)
        {
            return WindowsIndex.WindowsIndexSearchAsync(
                querySearchString,
                QueryConstructor.CreateQueryHelper,
                QueryConstructor.QueryForAllFilesAndFolders,
                Settings.IndexSearchExcludedSubdirectoryPaths,
                token);
        }

        private IAsyncEnumerable<SearchResult> WindowsIndexTopLevelFolderSearchAsync(string path,string search,
            CancellationToken token)
        {
            var queryConstructor = new QueryConstructor(Settings);

            return WindowsIndex.WindowsIndexSearchAsync(
                path,
                queryConstructor.CreateQueryHelper,
                queryConstructor.QueryForTopLevelDirectorySearch,
                Settings.IndexSearchExcludedSubdirectoryPaths,
                token);
        }
        public IAsyncEnumerable<SearchResult> SearchAsync(string search, CancellationToken token)
        {
            return WindowsIndexFilesAndFoldersSearchAsync(search, token);
        }
        public IAsyncEnumerable<SearchResult> ContentSearchAsync(string plainSearch, string contentSearch, CancellationToken token)
        {
            return WindowsIndexFileContentSearchAsync(contentSearch, token);
        }
        public IAsyncEnumerable<SearchResult> EnumerateAsync(string path, string search, bool recursive, CancellationToken token)
        {
            return recursive ? WindowsIndexFilesAndFoldersSearchAsync(search, token) : WindowsIndexTopLevelFolderSearchAsync(path, search, token);
        }
    }
}