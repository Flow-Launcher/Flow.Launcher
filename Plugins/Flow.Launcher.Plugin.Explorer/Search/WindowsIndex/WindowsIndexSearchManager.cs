using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Explorer.Exceptions;
using Flow.Launcher.Plugin.Explorer.Search.IProvider;

namespace Flow.Launcher.Plugin.Explorer.Search.WindowsIndex
{
    public class WindowsIndexSearchManager : IIndexProvider, IContentIndexProvider, IPathIndexProvider
    {
        private Settings Settings { get; }

        private QueryConstructor QueryConstructor { get; }
        
        public WindowsIndexSearchManager(Settings settings)
        {
            Settings = settings;
            QueryConstructor = new QueryConstructor(Settings);
        }

        private IAsyncEnumerable<SearchResult> WindowsIndexFileContentSearchAsync(
            ReadOnlySpan<char> querySearchString,
            CancellationToken token)
        {
            if (querySearchString.IsEmpty)
                return AsyncEnumerable.Empty<SearchResult>();

            try
            {
                return WindowsIndex.WindowsIndexSearchAsync(
                    QueryConstructor.CreateQueryHelper().ConnectionString,
                    QueryConstructor.FileContent(querySearchString),
                    token);
            }
            catch (COMException)
            {
                // Occurs when the Windows Indexing (WSearch) is turned off in services and unable to be used by Explorer plugin
                // Thrown by QueryConstructor.CreateQueryHelper()
                return HandledEngineNotAvailableExceptionAsync();
            }
        }

        private IAsyncEnumerable<SearchResult> WindowsIndexFilesAndFoldersSearchAsync(
            ReadOnlySpan<char> querySearchString,
            CancellationToken token = default)
        {
            try
            {
                return WindowsIndex.WindowsIndexSearchAsync(
                    QueryConstructor.CreateQueryHelper().ConnectionString,
                    QueryConstructor.FilesAndFolders(querySearchString),
                    token);
            }
            catch (COMException)
            {
                // Occurs when the Windows Indexing (WSearch) is turned off in services and unable to be used by Explorer plugin
                // Thrown by QueryConstructor.CreateQueryHelper()
                return HandledEngineNotAvailableExceptionAsync();
            }
        }

        private IAsyncEnumerable<SearchResult> WindowsIndexTopLevelFolderSearchAsync(
            ReadOnlySpan<char> search,
            ReadOnlySpan<char> path,
            bool recursive,
            CancellationToken token)
        {
            try
            {
                return WindowsIndex.WindowsIndexSearchAsync(
                    QueryConstructor.CreateQueryHelper().ConnectionString,
                    QueryConstructor.Directory(path, search, recursive),
                    token);
            }
            catch (COMException)
            {
                // Occurs when the Windows Indexing (WSearch) is turned off in services and unable to be used by Explorer plugin
                // Thrown by QueryConstructor.CreateQueryHelper()
                return HandledEngineNotAvailableExceptionAsync();
            }
        }
        public IAsyncEnumerable<SearchResult> SearchAsync(string search, CancellationToken token)
        {
            return WindowsIndexFilesAndFoldersSearchAsync(search, token: token);
        }
        public IAsyncEnumerable<SearchResult> ContentSearchAsync(string plainSearch, string contentSearch, CancellationToken token)
        {
            return WindowsIndexFileContentSearchAsync(contentSearch, token);
        }
        public IAsyncEnumerable<SearchResult> EnumerateAsync(string path, string search, bool recursive, CancellationToken token)
        {
            return WindowsIndexTopLevelFolderSearchAsync(search, path, recursive, token);
        }

        private IAsyncEnumerable<SearchResult> HandledEngineNotAvailableExceptionAsync()
        {
            if (!Settings.WarnWindowsSearchServiceOff)
                return AsyncEnumerable.Empty<SearchResult>();

            var api = Main.Context.API;

            throw new EngineNotAvailableException(
                "Windows Index",
                api.GetTranslation("plugin_explorer_windowsSearchServiceFix"),
                api.GetTranslation("plugin_explorer_windowsSearchServiceNotRunning"),
                Constants.WindowsIndexErrorImagePath,
                c =>
                {
                    Settings.WarnWindowsSearchServiceOff = false;

                    // Clears the warning message so user is not mistaken that it has not worked
                    api.ChangeQuery(string.Empty);

                    return ValueTask.FromResult(false);
                });
        }
    }
}
