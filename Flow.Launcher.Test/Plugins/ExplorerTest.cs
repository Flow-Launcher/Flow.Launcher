using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.Explorer;
using Flow.Launcher.Plugin.Explorer.Exceptions;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo;
using Flow.Launcher.Plugin.Explorer.Search.WindowsIndex;
using Flow.Launcher.Plugin.SharedCommands;
using Moq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Explorer.Search.Everything;
using Flow.Launcher.Plugin.Explorer.ViewModels;
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
        private static readonly PropertyInfo MainContextProperty = typeof(Main).GetProperty(
            "Context",
            BindingFlags.Static | BindingFlags.NonPublic);

        private bool PreviousLocationExistsReturnsTrue(string dummyString) => true;

        private bool PreviousLocationNotExistReturnsFalse(string dummyString) => false;

        private static PluginInitContext CreatePluginContext()
        {
            return CreatePluginContext(out _);
        }

        private static PluginInitContext CreatePluginContext(out Mock<IPublicAPI> api)
        {
            api = new Mock<IPublicAPI>();
            api.Setup(x => x.GetTranslation(It.IsAny<string>()))
                .Returns((string key) => key);
            return new PluginInitContext { API = api.Object };
        }

        private static object SetMainContext(PluginInitContext context)
        {
            var previousContext = MainContextProperty.GetValue(null);
            MainContextProperty.SetValue(null, context);
            return previousContext;
        }

        private static void RestoreMainContext(object context)
        {
            MainContextProperty.SetValue(null, context);
        }

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

            // The system running this test could have a different content locale than the hard-coded 1033 LCID en-US.
            var queryContentLocale = baseQuery.QueryContentLocale;
            expectedString = expectedString.Replace("1033", queryContentLocale.ToString(CultureInfo.InvariantCulture));

            // When
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

        [Test]
        public void GivenNonHomeFolderPaths_WhenCheckedWithIsHomeFolderPath_ThenShouldReturnFalse()
        {
            // Given
            var nonHomeFolders = new[]
            {
                @"C:\SomeRandomFolder",
                @"C:\Windows\System32",
                @"C:\Program Files",
            };

            // When, Then
            foreach (var folder in nonHomeFolders)
            {
                ClassicAssert.IsFalse(ResultManager.IsHomeFolderPath(folder),
                    $"Expected '{folder}' to NOT be recognized as a home folder");
            }
        }

        [Test]
        public void GivenPathsInsideHomeDirectories_WhenCheckedWithIsHomeFolderPath_ThenShouldReturnTrue()
{
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            
            if (string.IsNullOrEmpty(desktopPath))
            {
                Assert.Ignore("Desktop special folder path is unavailable in this environment.");
            }

            var homeFolderVariants = new[]
            {
                Path.Combine(desktopPath, "dummy_desktop_file"),
                Path.Combine(desktopPath, "dummy_desktop_folder") + "\\\\",
                desktopPath + "\\\\dummy_desktop_folder\\\\",
                Path.Combine(desktopPath, "dummy_desktop_folder", "dummy_desktop_file"),
            };

            foreach (var path in homeFolderVariants)
            {
                ClassicAssert.IsTrue(ResultManager.IsHomeFolderPath(path),
                    $"Expected '{path}' to be recognized as inside a home folder");
            }
        }

        [TestCase(Architecture.X64, "x64")]
        [TestCase(Architecture.X86, null)]
        [TestCase(Architecture.Arm, null)]
        [TestCase(Architecture.Arm64, null)]
        public void GivenProcessArchitecture_WhenLocatingEverythingSdk_ThenOnlyX64UsesBundledSdk(
            Architecture architecture,
            string expectedDirectory)
        {
            const string pluginDirectory = @"C:\Flow\Explorer";

            var result = EverythingSdkLocator.GetSdkDirectory(pluginDirectory, architecture);

            if (expectedDirectory is null)
            {
                ClassicAssert.IsNull(result);
            }
            else
            {
                ClassicAssert.AreEqual(
                    Path.Combine(pluginDirectory, "EverythingSDK", expectedDirectory),
                    result);
            }
        }

        [Test]
        public void GivenUnavailableEverythingSdk_WhenCheckingSortOption_ThenFailureIsExplicit()
        {
            var manager = new EverythingSearchManager(new Settings());
            manager.InitializeApi(null);

            Assert.Throws<PlatformNotSupportedException>(
                () => manager.IsFastSortOption(EverythingSortOption.NAME_ASCENDING));
        }

        [Test]
        public void GivenEverythingSdkLoadFailure_WhenCheckingSortOption_ThenSdkFailureIsPreserved()
        {
            var context = CreatePluginContext();
            var previousContext = SetMainContext(context);

            try
            {
                var settings = new Settings
                {
                    IndexSearchEngine = Settings.IndexSearchEngineOption.Everything,
                };
                var manager = (EverythingSearchManager)settings.IndexProvider;
                manager.InitializeApi(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

                var exception = Assert.Throws<DllNotFoundException>(
                    () => manager.IsFastSortOption(EverythingSortOption.NAME_ASCENDING));
                var viewModel = new SettingsViewModel(context, settings);

                ClassicAssert.IsInstanceOf<Win32Exception>(exception.InnerException);
                StringAssert.Contains(exception.InnerException.Message, exception.Message);
                ClassicAssert.AreEqual(System.Windows.Visibility.Visible, viewModel.FastSortWarningVisibility);
                StringAssert.Contains(exception.InnerException.Message, viewModel.SortOptionWarningMessage);
            }
            finally
            {
                RestoreMainContext(previousContext);
            }
        }

        [Test]
        public async Task GivenUnavailableEverythingSdk_WhenSelectingFallback_ThenSettingsViewModelUsesWindowsSearchAsync()
        {
            var context = CreatePluginContext(out var api);
            var previousContext = SetMainContext(context);
            try
            {
                var settings = new Settings
                {
                    IndexSearchEngine = Settings.IndexSearchEngineOption.Everything,
                    ContentSearchEngine = Settings.ContentIndexSearchEngineOption.Everything,
                    PathEnumerationEngine = Settings.PathEnumerationEngineOption.Everything,
                };
                var viewModel = new SettingsViewModel(context, settings);
                var changedProperties = new List<string>();
                viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
                var manager = new EverythingSearchManager(settings);
                manager.InitializeApi(null);

                ClassicAssert.AreEqual(System.Windows.Visibility.Visible, viewModel.FastSortWarningVisibility);
                ClassicAssert.IsNotEmpty(viewModel.SortOptionWarningMessage);

                var exception = Assert.ThrowsAsync<EngineNotAvailableException>(async () =>
                {
                    await foreach (var _ in manager.SearchAsync("test", CancellationToken.None))
                    {
                    }
                });

                ClassicAssert.IsNotNull(exception.Action);
                ClassicAssert.IsTrue(await exception.Action(new ActionContext()));
                ClassicAssert.AreEqual(Settings.IndexSearchEngineOption.WindowsIndex, settings.IndexSearchEngine);
                ClassicAssert.AreEqual(Settings.ContentIndexSearchEngineOption.WindowsIndex, settings.ContentSearchEngine);
                ClassicAssert.AreEqual(Settings.PathEnumerationEngineOption.WindowsIndex, settings.PathEnumerationEngine);
                ClassicAssert.AreEqual(Settings.IndexSearchEngineOption.WindowsIndex, viewModel.SelectedIndexSearchEngine.Value);
                ClassicAssert.AreEqual(Settings.ContentIndexSearchEngineOption.WindowsIndex, viewModel.SelectedContentSearchEngine.Value);
                ClassicAssert.AreEqual(Settings.PathEnumerationEngineOption.WindowsIndex, viewModel.SelectedPathEnumerationEngine.Value);
                CollectionAssert.Contains(changedProperties, nameof(SettingsViewModel.SelectedIndexSearchEngine));
                CollectionAssert.Contains(changedProperties, nameof(SettingsViewModel.SelectedContentSearchEngine));
                CollectionAssert.Contains(changedProperties, nameof(SettingsViewModel.SelectedPathEnumerationEngine));
                api.Verify(x => x.ReQuery(true), Times.Once);
            }
            finally
            {
                RestoreMainContext(previousContext);
            }
        }

        [Test]
        public void GivenUnavailablePathEngine_WhenSearchingRecursively_ThenOriginalStackTraceIsPreserved()
        {
            var directory = Directory.CreateTempSubdirectory("flow-launcher-stack-");
            var context = CreatePluginContext();
            var previousContext = SetMainContext(context);
            try
            {
                var settings = new Settings
                {
                    PathEnumerationEngine = Settings.PathEnumerationEngineOption.Everything,
                };
                ResultManager.Init(context, settings);
                var searchManager = new SearchManager(settings, context);
                var searchAsync = typeof(SearchManager).GetMethod(
                    "SearchAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var query = new Query
                {
                    ActionKeyword = Query.GlobalPluginWildcardSign,
                    Search = $"{directory.FullName}\\>missing",
                };

                var exception = Assert.ThrowsAsync<EngineNotAvailableException>(async () =>
                {
                    var task = (Task<List<Result>>)searchAsync.Invoke(
                        searchManager,
                        new object[] { query, CancellationToken.None });
                    await task;
                });

                StringAssert.Contains("EnsureAvailableAsync", exception.StackTrace);
            }
            finally
            {
                RestoreMainContext(previousContext);
                directory.Delete(true);
            }
        }

        [Test]
        public void GivenUnavailablePathEngine_WhenCreatingPathError_ThenFallbackExceptionIsPreserved()
        {
            var expected = new EngineNotAvailableException(
                "Everything",
                "Select Windows Search",
                "Everything is unavailable");

            var result = SearchManager.CreatePathEnumerationException(
                Settings.PathEnumerationEngineOption.Everything,
                expected);

            ClassicAssert.AreSame(expected, result);
            ClassicAssert.IsNotNull(((EngineNotAvailableException)result).Action);
        }
    }
}
