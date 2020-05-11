using NUnit.Framework;

namespace Flow.Launcher.Test.Plugins
{
    [TestFixture]
    public class ExplorerTest
    {
        public void GivenWindowsIndexSearch_WhenProvidedFolderPath_ThenQueryWhereRestrictionsShouldUseDirectoryString(string expectedString){}

        public void GivenWindowsIndexSearch_WhenSearchAllFoldersAndFiles_ThenQueryWhereRestrictionsShouldUseScopeString() { }

        public void GivenWindowsIndexSearch_WhenReturnedNilAndIsNotIndexed_ThenSearchMethodShouldContinueDirectoryInfoClassSearch() { }

        public void GivenWindowsIndexSearch_WhenSearchPatternHotKeyIsSearchAll_ThenQueryWhereRestrictionsShouldUseScopeString() { }
    }
}
