using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.Test
{
    [TestFixture]
    public class FuzzyMatcherTest
    {
        private const string Chrome = "Chrome";
        private const string CandyCrushSagaFromKing = "Candy Crush Saga from King";
        private const string HelpCureHopeRaiseOnMindEntityChrome = "Help cure hope raise on mind entity Chrome";
        private const string UninstallOrChangeProgramsOnYourComputer = "Uninstall or change programs on your computer";
        private const string LastIsChrome = "Last is chrome";
        private const string OneOneOneOne = "1111";
        private const string MicrosoftSqlServerManagementStudio = "Microsoft SQL Server Management Studio";
        private const string VisualStudioCode = "Visual Studio Code";

        public List<string> GetSearchStrings()
            => new List<string>
            {
                Chrome,
                "Choose which programs you want Windows to use for activities like web browsing, editing photos, sending e-mail, and playing music.",
                HelpCureHopeRaiseOnMindEntityChrome,
                CandyCrushSagaFromKing,
                UninstallOrChangeProgramsOnYourComputer,
                "Add, change, and manage fonts on your computer",
                LastIsChrome,
                OneOneOneOne
            };

        public List<int> GetPrecisionScores()
        {
            var listToReturn = new List<int>();

            Enum.GetValues(typeof(SearchPrecisionScore))
                .Cast<SearchPrecisionScore>()
                .ToList()
                .ForEach(x => listToReturn.Add((int)x));

            return listToReturn;
        }

        [Test]
        public void MatchTest()
        {
            var sources = new List<string>
            {
                "file open in browser-test",
                "Install Package",
                "add new bsd",
                "Inste",
                "aac"
            };

            var results = new List<Result>();
            var matcher = new FuzzyStringMatcher(new Settings());
            foreach (var str in sources)
            {
                results.Add(new Result
                {
                    Title = str, Score = matcher.FuzzyMatch("inst", str).RawScore
                });
            }

            results = results.Where(x => x.Score > 0).OrderByDescending(x => x.Score).ToList();

            Assert.IsTrue(results.Count == 3);
            Assert.IsTrue(results[0].Title == "Inste");
            Assert.IsTrue(results[1].Title == "Install Package");
            Assert.IsTrue(results[2].Title == "file open in browser-test");
        }

        [TestCase("Chrome")]
        public void WhenNotAllCharactersFoundInSearchString_ThenShouldReturnZeroScore(string searchString)
        {
            var compareString = "Can have rum only in my glass";
            var matcher = new FuzzyStringMatcher(new Settings());
            var scoreResult = matcher.FuzzyMatch(searchString, compareString).RawScore;

            Assert.True(scoreResult == 0);
        }

        [TestCase("chr")]
        [TestCase("chrom")]
        [TestCase("chrome")]
        [TestCase("cand")]
        [TestCase("cpywa")]
        [TestCase("ccs")]
        public void GivenQueryString_WhenAppliedPrecisionFiltering_ThenShouldReturnGreaterThanPrecisionScoreResults(
            string searchTerm)
        {
            var results = new List<Result>();
            var matcher = new FuzzyStringMatcher(new Settings());
            foreach (var str in GetSearchStrings())
            {
                results.Add(new Result
                {
                    Title = str, Score = matcher.FuzzyMatch(searchTerm, str).Score
                });
            }

            foreach (var precisionScore in GetPrecisionScores())
            {
                var filteredResult = results.Where(result => result.Score >= precisionScore)
                    .Select(result => result)
                    .OrderByDescending(x => x.Score)
                    .ToList();

                Debug.WriteLine("");
                Debug.WriteLine("###############################################");
                Debug.WriteLine("SEARCHTERM: " + searchTerm + ", GreaterThanSearchPrecisionScore: " + precisionScore);
                foreach (var item in filteredResult)
                {
                    Debug.WriteLine("SCORE: " + item.Score.ToString() + ", FoundString: " + item.Title);
                }

                Debug.WriteLine("###############################################");
                Debug.WriteLine("");

                Assert.IsFalse(filteredResult.Any(x => x.Score < precisionScore));
            }
        }

        [TestCase(Chrome, Chrome, 157)]
        [TestCase(Chrome, LastIsChrome, 147)]
        [TestCase("chro", HelpCureHopeRaiseOnMindEntityChrome, 50)]
        [TestCase("chr", HelpCureHopeRaiseOnMindEntityChrome, 30)]
        [TestCase(Chrome, UninstallOrChangeProgramsOnYourComputer, 21)]
        [TestCase(Chrome, CandyCrushSagaFromKing, 0)]
        [TestCase("sql", MicrosoftSqlServerManagementStudio, 110)]
        [TestCase("sql  manag", MicrosoftSqlServerManagementStudio, 121)] //double spacing intended
        public void WhenGivenQueryString_ThenShouldReturn_TheDesiredScoring(
            string queryString, string compareString, int expectedScore)
        {
            // When, Given
            var matcher = new FuzzyStringMatcher(new Settings());
            var rawScore = matcher.FuzzyMatch(queryString, compareString).RawScore;

            // Should
            Assert.AreEqual(expectedScore, rawScore,
                $"Expected score for compare string '{compareString}': {expectedScore}, Actual: {rawScore}");
        }

        [TestCase("goo", "Google Chrome", SearchPrecisionScore.Regular, true)]
        [TestCase("chr", "Google Chrome", SearchPrecisionScore.Low, true)]
        [TestCase("chr", "Chrome", SearchPrecisionScore.Regular, true)]
        [TestCase("chr", "Help cure hope raise on mind entity Chrome", SearchPrecisionScore.Regular, false)]
        [TestCase("chr", "Help cure hope raise on mind entity Chrome", SearchPrecisionScore.Low, true)]
        [TestCase("chr", "Candy Crush Saga from King", SearchPrecisionScore.Regular, false)]
        [TestCase("chr", "Candy Crush Saga from King", SearchPrecisionScore.None, true)]
        [TestCase("ccs", "Candy Crush Saga from King", SearchPrecisionScore.Low, true)]
        [TestCase("cand", "Candy Crush Saga from King", SearchPrecisionScore.Regular, true)]
        [TestCase("cand", "Help cure hope raise on mind entity Chrome", SearchPrecisionScore.Regular, false)]
        [TestCase("vsc", VisualStudioCode, SearchPrecisionScore.Regular, true)]
        [TestCase("vs", VisualStudioCode, SearchPrecisionScore.Regular, true)]
        [TestCase("vc", VisualStudioCode, SearchPrecisionScore.Regular, true)]
        [TestCase("vts", VisualStudioCode, SearchPrecisionScore.Regular, false)]
        [TestCase("vcs", VisualStudioCode, SearchPrecisionScore.Regular, false)]
        [TestCase("wt", "Windows Terminal From Microsoft Store", SearchPrecisionScore.Regular, false)]
        [TestCase("vsp", "Visual Studio 2019 Preview", SearchPrecisionScore.Regular, true)]
        [TestCase("vsp", "2019 Visual Studio Preview", SearchPrecisionScore.Regular, true)]
        [TestCase("2019p", "Visual Studio 2019 Preview", SearchPrecisionScore.Regular, true)]
        public void WhenGivenDesiredPrecision_ThenShouldReturn_AllResultsGreaterOrEqual(
            string queryString,
            string compareString,
            SearchPrecisionScore expectedPrecisionScore,
            bool expectedPrecisionResult)
        {        
            // When
            var settings = new Settings
            {
                QuerySearchPrecisionString = expectedPrecisionResult.ToString()
            };
            var matcher = new FuzzyStringMatcher(settings);

            // Given
            var matchResult = matcher.FuzzyMatch(queryString, compareString);

            Debug.WriteLine("");
            Debug.WriteLine("###############################################");
            Debug.WriteLine($"QueryString: {queryString}     CompareString: {compareString}");
            Debug.WriteLine(
                $"RAW SCORE: {matchResult.RawScore.ToString()}, PrecisionLevelSetAt: {expectedPrecisionScore} ({(int)expectedPrecisionScore})");
            Debug.WriteLine("###############################################");
            Debug.WriteLine("");

            // Should
            Assert.AreEqual(expectedPrecisionResult, matchResult.IsSearchPrecisionScoreMet(),
                $"Query: {queryString}{Environment.NewLine} " +
                $"Compare: {compareString}{Environment.NewLine}" +
                $"Raw Score: {matchResult.RawScore}{Environment.NewLine}" +
                $"Precision Score: {(int)expectedPrecisionScore}");
        }

        [TestCase("exce", "OverLeaf-Latex: An online LaTeX editor", SearchPrecisionScore.Regular, false)]
        [TestCase("term", "Windows Terminal (Preview)", SearchPrecisionScore.Regular, true)]
        [TestCase("sql s managa", MicrosoftSqlServerManagementStudio, SearchPrecisionScore.Regular, false)]
        [TestCase("sql' s manag", MicrosoftSqlServerManagementStudio, SearchPrecisionScore.Regular, false)]
        [TestCase("sql s manag", MicrosoftSqlServerManagementStudio, SearchPrecisionScore.Regular, true)]
        [TestCase("sql manag", MicrosoftSqlServerManagementStudio, SearchPrecisionScore.Regular, true)]
        [TestCase("sql", MicrosoftSqlServerManagementStudio, SearchPrecisionScore.Regular, true)]
        [TestCase("sql serv", MicrosoftSqlServerManagementStudio, SearchPrecisionScore.Regular, true)]
        [TestCase("servez", MicrosoftSqlServerManagementStudio, SearchPrecisionScore.Regular, false)]
        [TestCase("sql servz", MicrosoftSqlServerManagementStudio, SearchPrecisionScore.Regular, false)]
        [TestCase("sql serv man", MicrosoftSqlServerManagementStudio, SearchPrecisionScore.Regular, true)]
        [TestCase("sql studio", MicrosoftSqlServerManagementStudio, SearchPrecisionScore.Regular, true)]
        [TestCase("mic", MicrosoftSqlServerManagementStudio, SearchPrecisionScore.Regular, true)]
        [TestCase("mssms", MicrosoftSqlServerManagementStudio, SearchPrecisionScore.Regular, true)]
        [TestCase("msms", MicrosoftSqlServerManagementStudio, SearchPrecisionScore.Regular, true)]
        [TestCase("chr", "Shutdown", SearchPrecisionScore.Regular, false)]
        [TestCase("chr", "Change settings for text-to-speech and for speech recognition (if installed).", SearchPrecisionScore.Regular, false)]
        [TestCase("ch r", "Change settings for text-to-speech and for speech recognition (if installed).", SearchPrecisionScore.Regular, true)]
        [TestCase("a test", "This is a test", SearchPrecisionScore.Regular, true)]
        [TestCase("test", "This is a test", SearchPrecisionScore.Regular, true)]
        [TestCase("cod", VisualStudioCode, SearchPrecisionScore.Regular, true)]
        [TestCase("code", VisualStudioCode, SearchPrecisionScore.Regular, true)]
        [TestCase("codes", "Visual Studio Codes", SearchPrecisionScore.Regular, true)]
        public void WhenGivenQuery_ShouldReturnResults_ContainingAllQuerySubstrings(
            string queryString,
            string compareString,
            SearchPrecisionScore expectedPrecisionScore,
            bool expectedPrecisionResult)
        {
            // When
            var settings = new Settings
            {
                QuerySearchPrecisionString = expectedPrecisionResult.ToString()
            };
            var matcher = new FuzzyStringMatcher(settings);

            // Given
            var matchResult = matcher.FuzzyMatch(queryString, compareString);

            Debug.WriteLine("");
            Debug.WriteLine("###############################################");
            Debug.WriteLine($"QueryString: {queryString}     CompareString: {compareString}");
            Debug.WriteLine(
                $"RAW SCORE: {matchResult.RawScore.ToString()}, PrecisionLevelSetAt: {expectedPrecisionScore} ({(int)expectedPrecisionScore})");
            Debug.WriteLine("###############################################");
            Debug.WriteLine("");

            // Should
            Assert.AreEqual(expectedPrecisionResult, matchResult.IsSearchPrecisionScoreMet(),
                $"Query:{queryString}{Environment.NewLine} " +
                $"Compare:{compareString}{Environment.NewLine}" +
                $"Raw Score: {matchResult.RawScore}{Environment.NewLine}" +
                $"Precision Score: {(int)expectedPrecisionScore}");
        }

        [TestCase("man", "Task Manager", "eManual")]
        [TestCase("term", "Windows Terminal", "Character Map")]
        [TestCase("winterm", "Windows Terminal", "Cygwin64 Terminal")]
        public void WhenGivenAQuery_Scoring_ShouldGiveMoreWeightToStartOfNewWord(
            string queryString, string compareString1, string compareString2)
        {
            // When
            var matcher = new FuzzyStringMatcher(new Settings());

            // Given
            var compareString1Result = matcher.FuzzyMatch(queryString, compareString1);
            var compareString2Result = matcher.FuzzyMatch(queryString, compareString2);

            Debug.WriteLine("");
            Debug.WriteLine("###############################################");
            Debug.WriteLine($"QueryString: \"{queryString}\"{Environment.NewLine}");
            Debug.WriteLine(
                $"CompareString1: \"{compareString1}\", Score: {compareString1Result.Score}{Environment.NewLine}");
            Debug.WriteLine(
                $"CompareString2: \"{compareString2}\", Score: {compareString2Result.Score}{Environment.NewLine}");
            Debug.WriteLine("###############################################");
            Debug.WriteLine("");

            // Should
            Assert.True(compareString1Result.Score > compareString2Result.Score,
                $"Query: \"{queryString}\"{Environment.NewLine} " +
                $"CompareString1: \"{compareString1}\", Score: {compareString1Result.Score}{Environment.NewLine}" +
                $"Should be greater than{Environment.NewLine}" +
                $"CompareString2: \"{compareString2}\", Score: {compareString1Result.Score}{Environment.NewLine}");
        }

        [TestCase("vim", "Vim", "ignoreDescription", "ignore.exe", "Vim Diff", "ignoreDescription", "ignore.exe")]
        public void WhenMultipleResults_ExactMatchingResult_ShouldHaveGreatestScore(
            string queryString, string firstName, string firstDescription, string firstExecutableName,
            string secondName, string secondDescription, string secondExecutableName)
        {
            // Act
            var matcher = new FuzzyStringMatcher(new Settings());
            var firstNameMatch = matcher.FuzzyMatch(queryString, firstName).RawScore;
            var firstDescriptionMatch = matcher.FuzzyMatch(queryString, firstDescription).RawScore;
            var firstExecutableNameMatch = matcher.FuzzyMatch(queryString, firstExecutableName).RawScore;

            var secondNameMatch = matcher.FuzzyMatch(queryString, secondName).RawScore;
            var secondDescriptionMatch = matcher.FuzzyMatch(queryString, secondDescription).RawScore;
            var secondExecutableNameMatch = matcher.FuzzyMatch(queryString, secondExecutableName).RawScore;

            var firstScore = new[]
            {
                firstNameMatch, firstDescriptionMatch, firstExecutableNameMatch
            }.Max();
            var secondScore = new[]
            {
                secondNameMatch, secondDescriptionMatch, secondExecutableNameMatch
            }.Max();

            // Assert
            Assert.IsTrue(firstScore > secondScore,
                $"Query: \"{queryString}\"{Environment.NewLine} " +
                $"Name of first: \"{firstName}\", Final Score: {firstScore}{Environment.NewLine}" +
                $"Should be greater than{Environment.NewLine}" +
                $"Name of second: \"{secondName}\", Final Score: {secondScore}{Environment.NewLine}");
        }

        [TestCase("vsc", "Visual Studio Code", 100)]
        [TestCase("jbr", "JetBrain Rider", 100)]
        [TestCase("jr", "JetBrain Rider", 66)]
        [TestCase("vs", "Visual Studio", 100)]
        [TestCase("vs", "Visual Studio Preview", 66)]
        [TestCase("vsp", "Visual Studio Preview", 100)]
        [TestCase("pc", "postman canary", 100)]
        [TestCase("psc", "Postman super canary", 100)]
        [TestCase("psc", "Postman super Canary", 100)]
        [TestCase("vsp", "Visual Studio", 0)]
        [TestCase("vps", "Visual Studio", 0)]
        [TestCase(Chrome, HelpCureHopeRaiseOnMindEntityChrome, 75)]
        public void WhenGivenAnAcronymQuery_ShouldReturnAcronymScore(string queryString, string compareString,
            int desiredScore)
        {
            var matcher = new FuzzyStringMatcher(new Settings());
            var score = matcher.FuzzyMatch(queryString, compareString).Score;
            Assert.IsTrue(score == desiredScore,
                $@"Query: ""{queryString}""
                   CompareString: ""{compareString}""
                   Score: {score}
                   Desired Score: {desiredScore}");
        }
    }
}