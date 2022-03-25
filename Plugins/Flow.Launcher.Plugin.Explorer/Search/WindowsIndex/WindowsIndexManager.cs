using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Explorer.Search.WindowsIndex
{
    public class WindowsIndexManager : IIndexProvider
    {
        private Settings Settings { get; }
        private QueryConstructor QueryConstructor { get; }
        public WindowsIndexManager(Settings settings)
        {
            Settings = settings;
            QueryConstructor = new QueryConstructor(Settings);
        }


        private async Task<List<SearchResult>> WindowsIndexFileContentSearchAsync(Query query, string querySearchString,
            CancellationToken token)
        {
            if (string.IsNullOrEmpty(querySearchString))
                return new List<SearchResult>();

            return await WindowsIndex.WindowsIndexSearchAsync(
                querySearchString,
                QueryConstructor.CreateQueryHelper,
                QueryConstructor.QueryForFileContentSearch,
                Settings.IndexSearchExcludedSubdirectoryPaths,
                query,
                token).ConfigureAwait(false);
        }



        private async Task<List<SearchResult>> WindowsIndexFilesAndFoldersSearchAsync(Query query, string querySearchString,
            CancellationToken token)
        {
            return await WindowsIndex.WindowsIndexSearchAsync(
                querySearchString,
                QueryConstructor.CreateQueryHelper,
                QueryConstructor.QueryForAllFilesAndFolders,
                Settings.IndexSearchExcludedSubdirectoryPaths,
                query,
                token).ConfigureAwait(false);
        }
        
        
        private async Task<List<SearchResult>> WindowsIndexTopLevelFolderSearchAsync(Query query, string path,
            CancellationToken token)
        {
            var queryConstructor = new QueryConstructor(Settings);

            return await WindowsIndex.WindowsIndexSearchAsync(
                path,
                queryConstructor.CreateQueryHelper,
                queryConstructor.QueryForTopLevelDirectorySearch,
                Settings.IndexSearchExcludedSubdirectoryPaths,
                query,
                token).ConfigureAwait(false);
        }
        public ValueTask<IEnumerable<SearchResult>> SearchAsync(Query query, CancellationToken token)
        {
            return default;
        }
        public ValueTask<IEnumerable<SearchResult>> ContentSearchAsync(Query query, CancellationToken token)
        {
            return default;
        }
    }
}