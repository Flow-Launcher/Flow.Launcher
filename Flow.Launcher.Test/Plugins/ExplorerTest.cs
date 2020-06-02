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

        [TestCase]
        public void GivenTopLevelDirectorySearch_WhenIndexSearchNotRequired_ThenSearchMethodShouldContinueDirectoryInfoClassSearch() 
        {
            // Given
            var searchManager = new SearchManager(new Settings(), new PluginInitContext());
            
            // When
            var results = searchManager.TopLevelDirectorySearchBehaviour(
                                            MethodWindowsIndexSearchReturnsZeroResults, 
                                            MethodDirectoryInfoClassSearchReturnsTwoResults, 
                                            false, 
                                            new Query(),
                                            "string not used");

            // Then
            Assert.IsTrue(results.Count == 2,
                $"Expected to have 2 results from DirectoryInfoClassSearch {Environment.NewLine} " +
                $"Actual number of results is {results.Count} {Environment.NewLine}");
        }

        [TestCase]
        public void GivenTopLevelDirectorySearch_WhenIndexSearchNotRequired_ThenSearchMethodShouldNotContinueDirectoryInfoClassSearch()
        {
            // Given
            var searchManager = new SearchManager(new Settings(), new PluginInitContext());

            // When
            var results = searchManager.TopLevelDirectorySearchBehaviour(
                                            MethodWindowsIndexSearchReturnsZeroResults,
                                            MethodDirectoryInfoClassSearchReturnsTwoResults,
                                            true,
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
        [TestCase("RandomFile", false)]
        public void WhenGivenQuerySearchString_ThenShouldIndicateIfIsLocationPathString(string querySearchString, bool expectedResult)
        {
            // When, Given
            var result = FilesFolders.IsLocationPathString(querySearchString);

            //Then
            Assert.IsTrue(result == expectedResult,
                $"Expected query search string check result is: {expectedResult} {Environment.NewLine} " +
                $"Actual check result is {result} {Environment.NewLine}");

        }
        
        [TestCase(@"C:\SomeFolder\SomeApp", true, @"C:\SomeFolder\")]
        [TestCase(@"C:\SomeFolder\SomeApp\SomeFile", true, @"C:\SomeFolder\SomeApp\")]
        [TestCase(@"C:\NonExistentFolder\SomeApp", false, "")]
        public void GivenAPartialPath_WhenPreviousLevelDirectoryExists_ThenShouldReturnThePreviousDirectoryPathString(
            string path, bool previousDirectoryExists, string expectedString)
        {
            // When
            Func<string, bool> previousLocationExists = null;
            if (previousDirectoryExists)
            {
                previousLocationExists = PreviousLocationExistsReturnsTrue;
            }
            else
            {
                previousLocationExists = PreviousLocationNotExistReturnsFalse;
            }

            // Given
            var previousDirectoryPath = FilesFolders.GetPreviousExistingDirectory(previousLocationExists, path);

            //Then
            Assert.IsTrue(previousDirectoryPath == expectedString,
                $"Expected path string: {expectedString} {Environment.NewLine} " +
                $"Actual path string is {previousDirectoryPath} {Environment.NewLine}");
        }

        [TestCase(@"C:\NonExistentFolder\SomeApp", @"C:\NonExistentFolder\")]
        [TestCase(@"C:\NonExistentFolder\SomeApp\", @"C:\NonExistentFolder\SomeApp\")]
        public void WhenGivenAPath_ThenShouldReturnThePreviousDirectoryPathIfIncompleteOrOriginalString(
            string path, string expectedString)
        {
            var returnedPath = FilesFolders.ReturnPreviousDirectoryIfIncompleteString(path);

            //Then
            Assert.IsTrue(returnedPath == expectedString,
                $"Expected path string: {expectedString} {Environment.NewLine} " +
                $"Actual path string is {returnedPath} {Environment.NewLine}");
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

        [TestCase("c:\\somefolder\\>somefile","*somefile*")]
        [TestCase("c:\\somefolder\\somefile", "somefile*")]
        [TestCase("c:\\somefolder\\", "*")]
        public void GivenDirectoryInfoSearch_WhenSearchPatternHotKeyIsSearchAll_ThenSearchCriteriaShouldUseCriteriaString(string path, string expectedString)
        {
            // Given
            var criteriaConstructor = new DirectoryInfoSearch(new Settings());

            //When
            var resultString = criteriaConstructor.ConstructSearchCriteria(path);

            // Then
            Assert.IsTrue(resultString == expectedString,
                $"Expected criteria string: {expectedString}{Environment.NewLine} " +
                $"Actual criteria string was: {resultString}{Environment.NewLine}");
        }
    }
}
