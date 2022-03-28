using System.Collections.Generic;
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


        private async Task<List<SearchResult>> WindowsIndexFileContentSearchAsync(string querySearchString,
            CancellationToken token)
        {
            if (string.IsNullOrEmpty(querySearchString))
                return new List<SearchResult>();

            return await WindowsIndex.WindowsIndexSearchAsync(
                querySearchString,
                QueryConstructor.CreateQueryHelper,
                QueryConstructor.QueryForFileContentSearch,
                Settings.IndexSearchExcludedSubdirectoryPaths,
                token).ConfigureAwait(false);
        }



        private async Task<List<SearchResult>> WindowsIndexFilesAndFoldersSearchAsync(string querySearchString,
            CancellationToken token)
        {
            return await WindowsIndex.WindowsIndexSearchAsync(
                querySearchString,
                QueryConstructor.CreateQueryHelper,
                QueryConstructor.QueryForAllFilesAndFolders,
                Settings.IndexSearchExcludedSubdirectoryPaths,
                token).ConfigureAwait(false);
        }
        
        
        private async Task<List<SearchResult>> WindowsIndexTopLevelFolderSearchAsync(string path,string search,
            CancellationToken token)
        {
            var queryConstructor = new QueryConstructor(Settings);

            return await WindowsIndex.WindowsIndexSearchAsync(
                path,
                queryConstructor.CreateQueryHelper,
                queryConstructor.QueryForTopLevelDirectorySearch,
                Settings.IndexSearchExcludedSubdirectoryPaths,
                token).ConfigureAwait(false);
        }
        public async ValueTask<IEnumerable<SearchResult>> SearchAsync(string search, CancellationToken token)
        {
            return await WindowsIndexFilesAndFoldersSearchAsync(search, token);
        }
        public async ValueTask<IEnumerable<SearchResult>> ContentSearchAsync(string search, CancellationToken token)
        {
            return await WindowsIndexFileContentSearchAsync(search, token);
        }
        public async ValueTask<IEnumerable<SearchResult>> EnumerateAsync(string path, string search, bool recursive, CancellationToken token)
        {
            if(recursive)
                return await WindowsIndexFilesAndFoldersSearchAsync(search, token).ConfigureAwait(false);
            return await WindowsIndexTopLevelFolderSearchAsync(path, search, token);
        }
    }
}