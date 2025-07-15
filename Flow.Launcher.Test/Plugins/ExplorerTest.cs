using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.Explorer;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo;
using Flow.Launcher.Plugin.Explorer.Search.WindowsIndex;
using Flow.Launcher.Plugin.SharedCommands;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.IO;
using System.Runtime.Versioning;
using static Flow.Launcher.Plugin.Explorer.Search.SearchManager;

namespace Flow.Launcher.Test.Plugins
{
    /// <summary>
    /// These tests require the use of CSearchManager class from Microsoft.Search.Interop.
    /// Windows Search service needs to be running to complete the tests
    /// </summary>
    [TestFixture]
    public class ExplorerTest
    {
        private bool PreviousLocationExistsReturnsTrue(string dummyString) => true;

        private bool PreviousLocationNotExistReturnsFalse(string dummyString) => false;

        [SupportedOSPlatform("windows7.0")]
        [TestCase("C:\\SomeFolder\\", "directory='file:C:\\SomeFolder\\'")]
        public void GivenWindowsIndexSearch_WhenProvidedFolderPath_ThenQueryWhereRestrictionsShouldUseDirectoryString(string path, string expectedString)
        {
            // When
            var folderPath = path;
            var result = QueryConstructor.TopLevelDirectoryConstraint(folderPath);

            // Then
            ClassicAssert.IsTrue(result == expectedString,
                $"Expected QueryWhereRestrictions string: {expectedString}{Environment.NewLine} " +
                $"Actual: {result}{Environment.NewLine}");
        }

        [SupportedOSPlatform("windows7.0")]
        [TestCase("C:\\", $"SELECT TOP 100 System.FileName, System.ItemUrl, System.ItemType FROM SystemIndex WHERE directory='file:C:\\' ORDER BY {QueryConstructor.OrderIdentifier}")]
        [TestCase("C:\\SomeFolder\\", $"SELECT TOP 100 System.FileName, System.ItemUrl, System.ItemType FROM SystemIndex WHERE directory='file:C:\\SomeFolder\\' ORDER BY {QueryConstructor.OrderIdentifier}")]
        public void GivenWindowsIndexSearch_WhenSearchTypeIsTopLevelDirectorySearch_ThenQueryShouldUseExpectedString(string folderPath, string expectedString)
        {
            // Given
            var queryConstructor = new QueryConstructor(new Settings());

            //When            
            var queryString = queryConstructor.Directory(folderPath);

            // Then
            ClassicAssert.IsTrue(queryString.Replace("  ", " ") == expectedString.Replace("  ", " "),
                $"Expected string: {expectedString}{Environment.NewLine} " +
                $"Actual string was: {queryString}{Environment.NewLine}");
        }

        [SupportedOSPlatform("windows7.0")]
        [TestCase("C:\\SomeFolder", "flow.launcher.sln", "SELECT TOP 100 System.FileName, System.ItemUrl, System.ItemType" +
                                                         " FROM SystemIndex WHERE directory='file:C:\\SomeFolder'" +
                                                         " AND (System.FileName LIKE 'flow.launcher.sln%' OR CONTAINS(System.FileName,'\"flow.launcher.sln*\"'))" +
                                                         $" ORDER BY {QueryConstructor.OrderIdentifier}")]
        public void GivenWindowsIndexSearchTopLevelDirectory_WhenSearchingForSpecificItem_ThenQueryShouldUseExpectedString(
            string folderPath, string userSearchString, string expectedString)
        {
            // Given
            var queryConstructor = new QueryConstructor(new Settings());

            //When            
            var queryString = queryConstructor.Directory(folderPath, userSearchString);

            // Then
            ClassicAssert.AreEqual(expectedString, queryString);
        }

        [SupportedOSPlatform("windows7.0")]
        [TestCase("scope='file:'")]
        public void GivenWindowsIndexSearch_WhenSearchAllFoldersAndFiles_ThenQueryWhereRestrictionsShouldUseScopeString(string expectedString)
        {
            //When
            const string resultString = QueryConstructor.RestrictionsForAllFilesAndFoldersSearch;

            // Then
            ClassicAssert.AreEqual(expectedString, resultString);
        }

        [SupportedOSPlatform("windows7.0")]
        [TestCase("flow.launcher.sln", "SELECT TOP 100 \"System.FileName\", \"System.ItemUrl\", \"System.ItemType\" " +
                                       "FROM \"SystemIndex\" WHERE (System.FileName LIKE 'flow.launcher.sln%' " +
                                       $"OR CONTAINS(System.FileName,'\"flow.launcher.sln*\"',1033)) AND scope='file:' ORDER BY {QueryConstructor.OrderIdentifier}")]
        [TestCase("", $"SELECT TOP 100 \"System.FileName\", \"System.ItemUrl\", \"System.ItemType\" FROM \"SystemIndex\" WHERE WorkId IS NOT NULL AND scope='file:' ORDER BY {QueryConstructor.OrderIdentifier}")]
        public void GivenWindowsIndexSearch_WhenSearchAllFoldersAndFiles_ThenQueryShouldUseExpectedString(
            string userSearchString, string expectedString)
        {
            // Given
            var queryConstructor = new QueryConstructor(new Settings());
            var baseQuery = queryConstructor.CreateBaseQuery();

            // system running this test could have different locale than the hard-coded 1033 LCID en-US.
            var queryKeywordLocale = baseQuery.QueryKeywordLocale;
            expectedString = expectedString.Replace("1033", queryKeywordLocale.ToString());

            //When
            var resultString = queryConstructor.FilesAndFolders(userSearchString);

            // Then
            ClassicAssert.AreEqual(expectedString, resultString);
        }

        [SupportedOSPlatform("windows7.0")]
        [TestCase(@"some words", @"FREETEXT('some words')")]
        public void GivenWindowsIndexSearch_WhenQueryWhereRestrictionsIsForFileContentSearch_ThenShouldReturnFreeTextString(
            string querySearchString, string expectedString)
        {
            // Given
            _ = new QueryConstructor(new Settings());

            //When
            var resultString = QueryConstructor.RestrictionsForFileContentSearch(querySearchString);

            // Then
            ClassicAssert.IsTrue(resultString == expectedString,
                $"Expected QueryWhereRestrictions string: {expectedString}{Environment.NewLine} " +
                $"Actual string was: {resultString}{Environment.NewLine}");
        }

        [SupportedOSPlatform("windows7.0")]
        [TestCase("some words", "SELECT TOP 100 System.FileName, System.ItemUrl, System.ItemType " +
                                $"FROM SystemIndex WHERE FREETEXT('some words') AND scope='file:' ORDER BY {QueryConstructor.OrderIdentifier}")]
        public void GivenWindowsIndexSearch_WhenSearchForFileContent_ThenQueryShouldUseExpectedString(
            string userSearchString, string expectedString)
        {
            // Given
            var queryConstructor = new QueryConstructor(new Settings());

            //When
            var resultString = queryConstructor.FileContent(userSearchString);

            // Then
            ClassicAssert.IsTrue(resultString == expectedString,
                $"Expected query string: {expectedString}{Environment.NewLine} " +
                $"Actual string was: {resultString}{Environment.NewLine}");
        }

        public static void GivenQuery_WhenActionKeywordForFileContentSearchExists_ThenFileContentSearchRequiredShouldReturnTrue()
        {
            // Given
            var query = new Query
            {
                ActionKeyword = "doc:", Search = "search term"
            };

            var searchManager = new SearchManager(new Settings(), new PluginInitContext());

            // When
            var result = searchManager.IsFileContentSearch(query.ActionKeyword);

            // Then
            ClassicAssert.IsTrue(result,
                $"Expected True for file content search. {Environment.NewLine} " +
                $"Actual result was: {result}{Environment.NewLine}");
        }

        [TestCase(@"c:\\", false)]
        [TestCase(@"i:\", true)]
        [TestCase(@"\c:\", false)]
        [TestCase(@"cc:\", false)]
        [TestCase(@"\\\SomeNetworkLocation\", false)]
        [TestCase(@"\\SomeNetworkLocation\", true)]
        [TestCase("RandomFile", false)]
        [TestCase(@"c:\>*", true)]
        [TestCase(@"c:\>", true)]
        [TestCase(@"c:\SomeLocation\SomeOtherLocation\>", true)]
        [TestCase(@"c:\SomeLocation\SomeOtherLocation", true)]
        [TestCase(@"c:\SomeLocation\SomeOtherLocation\SomeFile.exe", true)]
        [TestCase(@"\\SomeNetworkLocation\SomeFile.exe", true)]

        public void WhenGivenQuerySearchString_ThenShouldIndicateIfIsLocationPathString(string querySearchString, bool expectedResult)
        {
            // When, Given
            var result = FilesFolders.IsLocationPathString(querySearchString);

            //Then
            ClassicAssert.IsTrue(result == expectedResult,
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
            ClassicAssert.IsTrue(previousDirectoryPath == expectedString,
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
            ClassicAssert.IsTrue(returnedPath == expectedString,
                $"Expected path string: {expectedString} {Environment.NewLine} " +
                $"Actual path string is {returnedPath} {Environment.NewLine}");
        }

        [SupportedOSPlatform("windows7.0")]
        [TestCase("c:\\SomeFolder", "scope='file:c:\\SomeFolder'")]
        [TestCase("c:\\OtherFolder", "scope='file:c:\\OtherFolder'")]
        public void GivenFilePath_WhenSearchPatternHotKeyIsSearchAll_ThenQueryWhereRestrictionsShouldUseScopeString(string path, string expectedString)
        {
            //When
            var resultString = QueryConstructor.RecursiveDirectoryConstraint(path);

            // Then
            ClassicAssert.AreEqual(expectedString, resultString);
        }

        [SupportedOSPlatform("windows7.0")]
        [TestCase("c:\\somefolder\\>somefile", "*somefile*")]
        [TestCase("c:\\somefolder\\somefile", "somefile*")]
        [TestCase("c:\\somefolder\\", "*")]
        public void GivenDirectoryInfoSearch_WhenSearchPatternHotKeyIsSearchAll_ThenSearchCriteriaShouldUseCriteriaString(string path, string expectedString)
        {

            //When
            var resultString = DirectoryInfoSearch.ConstructSearchCriteria(path);

            // Then
            ClassicAssert.AreEqual(expectedString, resultString);
        }

        [TestCase("c:\\somefolder\\someotherfolder", ResultType.Folder, "irrelevant", false, true, "c:\\somefolder\\someotherfolder\\")]
        [TestCase("c:\\somefolder\\someotherfolder\\", ResultType.Folder, "irrelevant", true, true, "c:\\somefolder\\someotherfolder\\")]
        [TestCase("c:\\somefolder\\someotherfolder", ResultType.Folder, "irrelevant", true, false, "p c:\\somefolder\\someotherfolder\\")]
        [TestCase("c:\\somefolder\\someotherfolder\\", ResultType.Folder, "irrelevant", false, false, "c:\\somefolder\\someotherfolder\\")]
        [TestCase("c:\\somefolder\\someotherfolder", ResultType.Folder, "p", true, false, "p c:\\somefolder\\someotherfolder\\")]
        [TestCase("c:\\somefolder\\someotherfolder", ResultType.Folder, "", true, true, "c:\\somefolder\\someotherfolder\\")]
        public void GivenFolderResult_WhenGetPath_ThenPathShouldBeExpectedString(
            string path, 
            ResultType type, 
            string actionKeyword,
            bool pathSearchKeywordEnabled, 
            bool searchActionKeywordEnabled,
            string expectedResult)
        {
            // Given
            var settings = new Settings() 
            {
                PathSearchKeywordEnabled = pathSearchKeywordEnabled,
                PathSearchActionKeyword = "p",
                SearchActionKeywordEnabled = searchActionKeywordEnabled,
                SearchActionKeyword = Query.GlobalPluginWildcardSign
            };
            ResultManager.Init(new PluginInitContext(), settings);
            
            // When
            var result = ResultManager.GetPathWithActionKeyword(path, type, actionKeyword);

            // Then
            ClassicAssert.AreEqual(result, expectedResult);
        }

        [TestCase("c:\\somefolder\\somefile", ResultType.File, "irrelevant", false, true, "e c:\\somefolder\\somefile")]
        [TestCase("c:\\somefolder\\somefile", ResultType.File, "p", true, false, "p c:\\somefolder\\somefile")]
        [TestCase("c:\\somefolder\\somefile", ResultType.File, "e", true, true, "e c:\\somefolder\\somefile")]
        [TestCase("c:\\somefolder\\somefile", ResultType.File, "irrelevant", false, false, "e c:\\somefolder\\somefile")]
        public void GivenFileResult_WhenGetPath_ThenPathShouldBeExpectedString(
            string path,
            ResultType type,
            string actionKeyword,
            bool pathSearchKeywordEnabled,
            bool searchActionKeywordEnabled,
            string expectedResult)
        {
            // Given
            var settings = new Settings()
            {
                PathSearchKeywordEnabled = pathSearchKeywordEnabled,
                PathSearchActionKeyword = "p",
                SearchActionKeywordEnabled = searchActionKeywordEnabled,
                SearchActionKeyword = "e"
            };
            ResultManager.Init(new PluginInitContext(), settings);

            // When
            var result = ResultManager.GetPathWithActionKeyword(path, type, actionKeyword);

            // Then
            ClassicAssert.AreEqual(result, expectedResult);
        }

        [TestCase("somefolder", "c:\\somefolder\\", ResultType.Folder, "q", false, false, "q somefolder")]
        [TestCase("somefolder", "c:\\somefolder\\", ResultType.Folder, "i", true, false, "p c:\\somefolder\\")]
        [TestCase("somefolder", "c:\\somefolder\\", ResultType.Folder, "irrelevant", true, true, "c:\\somefolder\\")]
        public void GivenQueryWithFolderTypeResult_WhenGetAutoComplete_ThenResultShouldBeExpectedString(
            string title,
            string path,
            ResultType resultType,
            string actionKeyword,
            bool pathSearchKeywordEnabled,
            bool searchActionKeywordEnabled,
            string expectedResult)
        {
            // Given
            var query = new Query() { ActionKeyword = actionKeyword };
            var settings = new Settings()
            {
                PathSearchKeywordEnabled = pathSearchKeywordEnabled,
                PathSearchActionKeyword = "p",
                SearchActionKeywordEnabled = searchActionKeywordEnabled,
                SearchActionKeyword = Query.GlobalPluginWildcardSign,
                QuickAccessActionKeyword = "q",
                IndexSearchActionKeyword = "i"
            };
            ResultManager.Init(new PluginInitContext(), settings);

            // When
            var result = ResultManager.GetAutoCompleteText(title, query, path, resultType);

            // Then
            ClassicAssert.AreEqual(result, expectedResult);
        }

        [TestCase("somefile", "c:\\somefolder\\somefile", ResultType.File, "q", false, false, "q somefile")]
        [TestCase("somefile", "c:\\somefolder\\somefile", ResultType.File, "i", true, false, "p c:\\somefolder\\somefile")]
        [TestCase("somefile", "c:\\somefolder\\somefile", ResultType.File, "irrelevant", true, true, "c:\\somefolder\\somefile")]
        public void GivenQueryWithFileTypeResult_WhenGetAutoComplete_ThenResultShouldBeExpectedString(
            string title,
            string path,
            ResultType resultType,
            string actionKeyword,
            bool pathSearchKeywordEnabled,
            bool searchActionKeywordEnabled,
            string expectedResult)
        {
            // Given
            var query = new Query() { ActionKeyword = actionKeyword };
            var settings = new Settings()
            {
                QuickAccessActionKeyword = "q",
                IndexSearchActionKeyword = "i",
                PathSearchActionKeyword = "p",
                PathSearchKeywordEnabled = pathSearchKeywordEnabled,
                SearchActionKeywordEnabled = searchActionKeywordEnabled,
                SearchActionKeyword = Query.GlobalPluginWildcardSign
            };
            ResultManager.Init(new PluginInitContext(), settings);

            // When
            var result = ResultManager.GetAutoCompleteText(title, query, path, resultType);

            // Then
            ClassicAssert.AreEqual(result, expectedResult);
        }

        [TestCase(@"c:\foo", @"c:\foo", true)]
        [TestCase(@"C:\Foo\", @"c:\foo\", true)]
        [TestCase(@"c:\foo", @"c:\foo\", false)]
        public void GivenTwoPaths_WhenCompared_ThenShouldBeExpectedSameOrDifferent(string path1, string path2, bool expectedResult)
        {
            // Given
            var comparator = PathEqualityComparator.Instance;
            var result1 = new Result
            {
                Title = Path.GetFileName(path1),
                SubTitle = path1
            };
            var result2 = new Result
            {
                Title = Path.GetFileName(path2),
                SubTitle = path2
            };

            // When, Then
            ClassicAssert.AreEqual(expectedResult, comparator.Equals(result1, result2));
        }

        [TestCase(@"c:\foo\", @"c:\foo\")]
        [TestCase(@"C:\Foo\", @"c:\foo\")]
        public void GivenTwoPaths_WhenComparedHasCode_ThenShouldBeSame(string path1, string path2)
        {
            // Given
            var comparator = PathEqualityComparator.Instance;
            var result1 = new Result
            {
                Title = Path.GetFileName(path1),
                SubTitle = path1
            };
            var result2 = new Result
            {
                Title = Path.GetFileName(path2),
                SubTitle = path2
            };

            var hash1 = comparator.GetHashCode(result1);
            var hash2 = comparator.GetHashCode(result2);

            // When, Then
            ClassicAssert.IsTrue(hash1 == hash2);
        }

        [TestCase(@"%appdata%", true)]
        [TestCase(@"%appdata%\123", true)]
        [TestCase(@"c:\foo %appdata%\", false)]
        [TestCase(@"c:\users\%USERNAME%\downloads", true)]
        [TestCase(@"c:\downloads", false)]
        [TestCase(@"%", false)]
        [TestCase(@"%%", false)]
        [TestCase(@"%bla%blabla%", false)]
        public void GivenPath_WhenHavingEnvironmentVariableOrNot_ThenShouldBeExpected(string path, bool expectedResult)
        {
            // When
            var result = EnvironmentVariables.HasEnvironmentVar(path);

            // Then
            ClassicAssert.AreEqual(result, expectedResult);
        }
    }
}
