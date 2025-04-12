using System.Collections.Generic;

namespace Flow.Launcher.Plugin.SharedModels
{
    /// <summary>
    /// Represents the result of a match operation.
    /// </summary>
    public class MatchResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MatchResult"/> class.
        /// </summary>
        /// <param name="success"></param>
        /// <param name="searchPrecision"></param>
        public MatchResult(bool success, SearchPrecisionScore searchPrecision)
        {
            Success = success;
            SearchPrecision = searchPrecision;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MatchResult"/> class.
        /// </summary>
        /// <param name="success"></param>
        /// <param name="searchPrecision"></param>
        /// <param name="matchData"></param>
        /// <param name="rawScore"></param>
        public MatchResult(bool success, SearchPrecisionScore searchPrecision, List<int> matchData, int rawScore)
        {
            Success = success;
            SearchPrecision = searchPrecision;
            MatchData = matchData;
            RawScore = rawScore;
        }

        /// <summary>
        /// Whether the match operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The final score of the match result with search precision filters applied.
        /// </summary>
        public int Score { get; set; }

        /// <summary>
        /// The raw calculated search score without any search precision filtering applied.
        /// </summary>
        private int _rawScore;

        /// <summary>
        /// The raw calculated search score without any search precision filtering applied.
        /// </summary>
        public int RawScore
        {
            get { return _rawScore; }
            set
            {
                _rawScore = value;
                Score = ScoreAfterSearchPrecisionFilter(_rawScore);
            }
        }

        /// <summary>
        /// Matched data to highlight.
        /// </summary>
        public List<int> MatchData { get; set; }

        /// <summary>
        /// The search precision score used to filter the search results.
        /// </summary>
        public SearchPrecisionScore SearchPrecision { get; set; }

        /// <summary>
        /// Determines if the search precision score is met.
        /// </summary>
        /// <returns></returns>
        public bool IsSearchPrecisionScoreMet()
        {
            return IsSearchPrecisionScoreMet(_rawScore);
        }

        private bool IsSearchPrecisionScoreMet(int rawScore)
        {
            return rawScore >= (int)SearchPrecision;
        }

        private int ScoreAfterSearchPrecisionFilter(int rawScore)
        {
            return IsSearchPrecisionScoreMet(rawScore) ? rawScore : 0;
        }
    }

    /// <summary>
    /// Represents the search precision score used to filter search results.
    /// </summary>
    public enum SearchPrecisionScore
    {
        /// <summary>
        /// The highest search precision score.
        /// </summary>
        Regular = 50,

        /// <summary>
        /// The medium search precision score.
        /// </summary>
        Low = 20,

        /// <summary>
        /// The lowest search precision score.
        /// </summary>
        None = 0
    }
}
