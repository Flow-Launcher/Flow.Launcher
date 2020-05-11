using Flow.Launcher.Plugin.Explorer;
using Flow.Launcher.Plugin.Explorer.Search.WindowsIndex;
using NUnit.Framework;
using System;

namespace Flow.Launcher.Test.Plugins
{
    [TestFixture]
    public class ExplorerTest
    {
        [TestCase("directory='file:{path}'")]
        public void GivenWindowsIndexSearch_WhenProvidedFolderPath_ThenQueryWhereRestrictionsShouldUseDirectoryString(string expectedString)
        {
            // Given
            var queryConstructor = new QueryConstructor(new Settings());

            // When
            var path = @"C:\Dropbox";
            var result = queryConstructor.QueryWhereRestrictionsForTopLevelDirectorySearch(path);

            // Then
            Assert.IsTrue(result == expectedString,
                $"Expected QueryWhereRestrictions string: {expectedString}{Environment.NewLine} " +
                $"Actual: {result}{Environment.NewLine}");
        }

        public void GivenWindowsIndexSearch_WhenSearchAllFoldersAndFiles_ThenQueryWhereRestrictionsShouldUseScopeString() { }

        public void GivenWindowsIndexSearch_WhenReturnedNilAndIsNotIndexed_ThenSearchMethodShouldContinueDirectoryInfoClassSearch() { }

        public void GivenWindowsIndexSearch_WhenSearchPatternHotKeyIsSearchAll_ThenQueryWhereRestrictionsShouldUseScopeString() { }
    }
}
