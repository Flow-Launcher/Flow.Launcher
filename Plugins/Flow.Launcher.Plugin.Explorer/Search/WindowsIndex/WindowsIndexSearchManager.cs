using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Explorer.Search.IProvider;
using Microsoft.Search.Interop;

namespace Flow.Launcher.Plugin.Explorer.Search.WindowsIndex
{
    public class WindowsIndexSearchManager : IIndexProvider, IContentIndexProvider, IPathIndexProvider
    {
        private Settings Settings { get; }
        private QueryConstructor QueryConstructor { get; }

        private CSearchQueryHelper QueryHelper { get; }
        public WindowsIndexSearchManager(Settings settings)
        {
            Settings = settings;
            QueryConstructor = new QueryConstructor(Settings);
            QueryHelper = QueryConstructor.CreateQueryHelper();
        }

        private IAsyncEnumerable<SearchResult> WindowsIndexFileContentSearchAsync(ReadOnlySpan<char> querySearchString,
            CancellationToken token)
        {
            if (querySearchString.IsEmpty)
                return AsyncEnumerable.Empty<SearchResult>();

            return WindowsIndex.WindowsIndexSearchAsync(
                QueryHelper.ConnectionString,
                QueryConstructor.FileContent(querySearchString),
                token);
        }

        private IAsyncEnumerable<SearchResult> WindowsIndexFilesAndFoldersSearchAsync(ReadOnlySpan<char> querySearchString,
            CancellationToken token = default)
        {
            return WindowsIndex.WindowsIndexSearchAsync(
                QueryHelper.ConnectionString,
                QueryConstructor.FilesAndFolders(querySearchString),
                token);
        }

        private IAsyncEnumerable<SearchResult> WindowsIndexTopLevelFolderSearchAsync(ReadOnlySpan<char> search,
            ReadOnlySpan<char> path,
            bool recursive,
            CancellationToken token)
        {
            var queryConstructor = new QueryConstructor(Settings);

            return WindowsIndex.WindowsIndexSearchAsync(
                QueryConstructor.CreateQueryHelper().ConnectionString,
                queryConstructor.Directory(path, search, recursive),
                token);
        }
        public IAsyncEnumerable<SearchResult> SearchAsync(ReadOnlySpan<char> search, CancellationToken token)
        {
            return WindowsIndexFilesAndFoldersSearchAsync(search, token: token);
        }
        public IAsyncEnumerable<SearchResult> ContentSearchAsync(ReadOnlySpan<char> plainSearch, ReadOnlySpan<char> contentSearch, CancellationToken token)
        {
            return WindowsIndexFileContentSearchAsync(contentSearch, token);
        }
        public IAsyncEnumerable<SearchResult> EnumerateAsync(ReadOnlySpan<char> path, ReadOnlySpan<char> search, bool recursive, CancellationToken token)
        {
            return WindowsIndexTopLevelFolderSearchAsync(search, path, recursive, token);
        }
    }
}
