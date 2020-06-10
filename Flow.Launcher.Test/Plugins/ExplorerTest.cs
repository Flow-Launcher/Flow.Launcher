using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.Explorer;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo;
using Flow.Launcher.Plugin.Explorer.Search.WindowsIndex;
using Flow.Launcher.Plugin.SharedCommands;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Flow.Launcher.Test.Plugins
{
    [TestFixture]
    public class ExplorerTest
    {
        private List<Result> MethodWindowsIndexSearchReturnsZeroResults(Query dummyQuery, string dummyString)
        {
            return new List<Result>();
        }

        private List<Result> MethodDirectoryInfoClassSearchReturnsTwoResults(Query dummyQuery, string dummyString)
        {
            return new List<Result> 
            { 
                new Result
                {
                    Title="Result 1"
                },

                new Result
                {
                    Title="Result 2"
                }
            };
        }

        private bool PreviousLocationExistsReturnsTrue(string dummyString) => true;

        private bool PreviousLocationNotExistReturnsFalse(string dummyString) => false;

        [TestCase("C:\\SomeFolder\\", "directory='file:C:\\SomeFolder\\'")]
        public void GivenWindowsIndexSearch_WhenProvidedFolderPath_ThenQueryWhereRestrictionsShouldUseDirectoryString(string path, string expectedString)
        {
            // Given
            var queryConstructor = new QueryConstructor(new Settings());

            // When
            var folderPath = path;
            var result = queryConstructor.QueryWhereRestrictionsForTopLevelDirectorySearch(folderPath);

            // Then
            Assert.IsTrue(result == expectedString,
                $"Expected QueryWhereRestrictions string: {expectedString}{Environment.NewLine} " +
                $"Actual: {result}{Environment.NewLine}");
        }

        [TestCase("C:\\", "SELECT TOP 100 System.FileName, System.ItemPathDisplay, System.ItemType FROM SystemIndex WHERE directory='file:C:\\'")]
        [TestCase("C:\\SomeFolder\\", "SELECT TOP 100 System.FileName, System.ItemPathDisplay, System.ItemType FROM SystemIndex WHERE directory='file:C:\\SomeFolder\\'")]
        public void GivenWindowsIndexSearch_WhenSearchTypeIsTopLevelDirectorySearch_ThenQueryShouldUseExpectedString(string folderPath, string expectedString)
        {
            // Given
            var queryConstructor = new QueryConstructor(new Settings());
            
            //When            
            var queryString = queryConstructor.QueryForTopLevelDirectorySearch(folderPath);
            
            // Then
            Assert.IsTrue(queryString == expectedString,
                $"Expected string: {expectedString}{Environment.NewLine} " +
                $"Actual string was: {queryString}{Environment.NewLine}");
        }

        [TestCase("C:\\SomeFolder\\flow.launcher.sln", "SELECT TOP 100 System.FileName, System.ItemPathDisplay, System.ItemType " +
            "FROM SystemIndex WHERE (System.FileName LIKE 'flow.launcher.sln%' " +
                                        "OR CONTAINS(System.FileName,'\"flow.launcher.sln*\"',1033))" +
                                        " AND directory='file:C:\\SomeFolder'")]
        public void GivenWindowsIndexSearchTopLevelDirectory_WhenSearchingForSpecificItem_ThenQueryShouldUseExpectedString(
            string userSearchString, string expectedString)
        {
            // Given
            var queryConstructor = new QueryConstructor(new Settings());

            //When            
            var queryString = queryConstructor.QueryForTopLevelDirectorySearch(userSearchString);

            // Then
            Assert.IsTrue(queryString == expectedString,
                $"Expected string: {expectedString}{Environment.NewLine} " +
                $"Actual string was: {queryString}{Environment.NewLine}");
        }

        [TestCase("C:\\SomeFolder\\SomeApp", "(System.FileName LIKE 'SomeApp%' " +
                    "OR CONTAINS(System.FileName,'\"SomeApp*\"',1033))" +
                    " AND directory='file:C:\\SomeFolder'")]
        public void GivenWindowsIndexSearchTopLevelDirectory_WhenSearchingForSpecificItem_ThenQueryWhereRestrictionsShouldUseDirectoryString(
            string userSearchString, string expectedString)
        {
            // Given
            var queryConstructor = new QueryConstructor(new Settings());

            //When            
            var queryString = queryConstructor.QueryWhereRestrictionsForTopLevelDirectorySearch(userSearchString);

            // Then
            Assert.IsTrue(queryString == expectedString,
                $"Expected string: {expectedString}{Environment.NewLine} " +
                $"Actual string was: {queryString}{Environment.NewLine}");
        }

        [TestCase("scope='file:'")]
        public void GivenWindowsIndexSearch_WhenSearchAllFoldersAndFiles_ThenQueryWhereRestrictionsShouldUseScopeString(string expectedString) 
        {
            // Given
            var queryConstructor = new QueryConstructor(new Settings());

            //When
            var resultString = queryConstructor.QueryWhereRestrictionsForAllFilesAndFoldersSearch();

            // Then
            Assert.IsTrue(resultString == expectedString,
                $"Expected QueryWhereRestrictions string: {expectedString}{Environment.NewLine} " +
                $"Actual string was: {resultString}{Environment.NewLine}");
        }

        [TestCase("flow.launcher.sln", "SELECT TOP 100 \"System.FileName\", \"System.ItemPathDisplay\", \"System.ItemType\" " +
            "FROM \"SystemIndex\" WHERE (System.FileName LIKE 'flow.launcher.sln%' " +
                                        "OR CONTAINS(System.FileName,'\"flow.launcher.sln*\"',1033)) AND scope='file:'")]
        public void GivenWindowsIndexSearch_WhenSearchAllFoldersAndFiles_ThenQueryShouldUseExpectedString(
            string userSearchString, string expectedString) 
        {
            // Given
            var queryConstructor = new QueryConstructor(new Settings());

            //When
            var resultString = queryConstructor.QueryForAllFilesAndFolders(userSearchString);

            // Then
            Assert.IsTrue(resultString == expectedString,
                $"Expected query string: {expectedString}{Environment.NewLine} " +
                $"Actual string was: {resultString}{Environment.NewLine}");
        }

        [TestCase("c:\\SomeFolder\\>", "scope='file:c:\\SomeFolder'")]
        [TestCase("c:\\SomeFolder\\>SomeName", "(System.FileName LIKE 'SomeName%' " +
                                                        "OR CONTAINS(System.FileName,'\"SomeName*\"',1033)) AND " +
                                                        "scope='file:c:\\SomeFolder'")]
        public void GivenWindowsIndexSearch_WhenSearchPatternHotKeyIsSearchAll_ThenQueryWhereRestrictionsShouldUseScopeString(string path, string expectedString) 
        {
            // Given
            var queryConstructor = new QueryConstructor(new Settings());

            //When
            var resultString = queryConstructor.QueryWhereRestrictionsForTopLevelDirectoryAllFilesAndFoldersSearch(path);

            // Then
            Assert.IsTrue(resultString == expectedString,
                $"Expected QueryWhereRestrictions string: {expectedString}{Environment.NewLine} " +
                $"Actual string was: {resultString}{Environment.NewLine}");
        }
    }
}
