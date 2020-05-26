using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.Explorer;
using Flow.Launcher.Plugin.Explorer.Search;
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

        private bool MethodIndexExistsReturnsTrue(string dummyString) => true;

        private bool MethodIndexExistsReturnsFalse(string dummyString) => false;

        private bool LocationExistsReturnsTrue(string dummyString) => true;

        private bool LocationNotExistReturnsFalse(string dummyString) => false;

        [TestCase("C:\\Dropbox", "directory='file:C:\\Dropbox'")]
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

        [TestCase("C:\\Dropbox", "SELECT TOP 100 System.FileName, System.ItemPathDisplay, System.ItemType FROM SystemIndex WHERE directory='file:C:\\Dropbox'")]
        public void GivenWindowsIndexSearch_WhenSearchTypeIsSearchTopFolderLevel_ThenQueryShouldUseExpectedString(string folderPath, string expectedString)
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

        [TestCase("flow.launcher.sln", "SELECT TOP 100 \"System.FileName\", \"System.ItemPathDisplay\", \"System.ItemType\" " +
            "FROM \"SystemIndex\" WHERE (System.FileName LIKE 'flow.launcher.sln%' " +
                                        "OR CONTAINS(System.FileName,'\"flow.launcher.sln*\"',1033))" +
                                        " AND directory='file:C:\\Dropbox'")]
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

        [TestCase("C:\\Dropbox\\App", "WHERE (System.FileName LIKE 'App%' " +
                    "OR CONTAINS(System.FileName,'\"App*\"',1033))" +
                    " AND directory='file:C:\\Dropbox'")]
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

        [TestCase]
        public void GivenWindowsIndexSearch_WhenReturnedZeroResultsAndIsNotIndexed_ThenSearchMethodShouldContinueDirectoryInfoClassSearch() 
        {
            // Given
            var searchManager = new SearchManager(new Settings(), new PluginInitContext());
            
            // When
            var results = searchManager.TopLevelFolderSearchBehaviour(
                                            MethodWindowsIndexSearchReturnsZeroResults, 
                                            MethodDirectoryInfoClassSearchReturnsTwoResults, 
                                            MethodIndexExistsReturnsFalse, 
                                            new Query(),
                                            "string not used");

            // Then
            Assert.IsTrue(results.Count == 2,
                $"Expected to have 2 results from DirectoryInfoClassSearch {Environment.NewLine} " +
                $"Actual number of results is {results.Count} {Environment.NewLine}");
        }

        [TestCase]
        public void GivenWindowsIndexSearch_WhenReturnedZeroResultsAndIsIndexed_ThenSearchMethodShouldNotContinueDirectoryInfoClassSearch()
        {
            // Given
            var searchManager = new SearchManager(new Settings(), new PluginInitContext());

            // When
            var results = searchManager.TopLevelFolderSearchBehaviour(
                                            MethodWindowsIndexSearchReturnsZeroResults,
                                            MethodDirectoryInfoClassSearchReturnsTwoResults,
                                            MethodIndexExistsReturnsTrue,
                                            new Query(),
                                            "string not used");

            // Then
            Assert.IsTrue(results.Count == 0,
                $"Expected to have 0 results because location is indexed {Environment.NewLine} " +
                $"Actual number of results is {results.Count} {Environment.NewLine}");
        }

        [TestCase(@"c:\\", false)]
        [TestCase(@"i:\", true)]
        [TestCase(@"\c:\", false)]
        [TestCase(@"cc:\", false)]
        [TestCase(@"\\\SomeNetworkLocation\", false)]
        [TestCase("RandomString", false)]
        public void WhenGivenQuerySearchString_ThenShouldIndicateIfItIsLocationString(string querySearchString, bool expectedResult)
        {
            // When, Given
            var result = FilesFolders.IsLocationPathString(querySearchString);

            //Then
            Assert.IsTrue(result == expectedResult,
                $"Expected query search string check result is: {expectedResult} {Environment.NewLine} " +
                $"Actual check result is {result} {Environment.NewLine}");

        }
        
        [TestCase(@"C:\Dropbox\Drop", @"C:\Dropbox")]
        [TestCase(@"C:\Dropbox\Drop\App", @"C:\Dropbox\Drop")]
        public void GivenAPartialPath_WhenPreviousLevelDirectoryExists_ThenShouldReturnThePreviousDirectoryPathString()
        {

        }

        [TestCase(@"C:\Dropbox\Drop", "")]
        public void GivenAPartialPath_WhenPreviousLevelDirectoryNotExists_ThenShouldReturnEmptyString()
        {

        }

        public void GivenWindowsIndexSearch_WhenSearchPatternHotKeyIsSearchAll_ThenQueryWhereRestrictionsShouldUseScopeString() { }
    }
}
