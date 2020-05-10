using NUnit.Framework;

namespace Flow.Launcher.Test.Plugins
{
    [TestFixture]
    public class ExplorerTest
    {
        public void WhenGivenFolderPathWindowsIndexSearchQuery_QueryStringShouldUseDirectoryCommand() { }

        public void WhenSearchAllFoldersAndFilesWindowsIndexSearchQuery_QueryStringShouldUseScopeCommand() { }

        public void WhenWindowsIndexSearchReturnedNilAndIsNotIndexed_NewSearchMethodShouldUseDirectoryInfoClass() { }

        public void WhenSearchPatternHotKeyIsSearchAll_WindowsIndexSearchQueryStringShouldUseScopeCommand() { }
    }
}
