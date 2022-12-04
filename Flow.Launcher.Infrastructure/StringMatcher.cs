using Flow.Launcher.Plugin.SharedModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Infrastructure
{
    public class StringMatcher
    {
        private readonly MatchOption _defaultMatchOption = new MatchOption();

        public SearchPrecisionScore UserSettingSearchPrecision { get; set; }

        private readonly IAlphabet _alphabet;

        public StringMatcher(IAlphabet alphabet = null)
        {
            _alphabet = alphabet;
        }

        public static StringMatcher Instance { get; internal set; }

        public static MatchResult FuzzySearch(string query, string stringToCompare)
        {
            return Instance.FuzzyMatch(query, stringToCompare);
        }

        public MatchResult FuzzyMatch(string query, string stringToCompare)
        {
            return FuzzyMatch(query, stringToCompare, _defaultMatchOption);
        }

        /// <summary>
        /// Current method has two parts, Acronym Match and Fuzzy Search:
        /// 
        /// Acronym Match:
        /// Charater listed below will be considered as acronym
        /// 1. Character on index 0
        /// 2. Character appears after a space
        /// 3. Character that is UpperCase
        /// 4. Character that is number
        /// 
        /// Acronym Match will succeed when all query characters match with acronyms in stringToCompare.
        /// If any of the characters in the query isn't matched with stringToCompare, Acronym Match will fail.
        /// Score will be calculated based the percentage of all query characters matched with total acronyms in stringToCompare.
        /// 
        /// Fuzzy Search:
        /// Character matching + substring matching;
        /// 1. Query search string is split into substrings, separator is whitespace.
        /// 2. Check each query substring's characters against full compare string,
        /// 3. if a character in the substring is matched, loop back to verify the previous character.
        /// 4. If previous character also matches, and is the start of the substring, update list.
        /// 5. Once the previous character is verified, move on to the next character in the query substring.
        /// 6. Move onto the next substring's characters until all substrings are checked.
        /// 7. Consider success and move onto scoring if every char or substring without whitespaces matched
        /// </summary>
        public MatchResult FuzzyMatch(string query, string stringToCompare, MatchOption opt)
        {
            if (string.IsNullOrEmpty(stringToCompare) || string.IsNullOrEmpty(query))
                return new MatchResult(false, UserSettingSearchPrecision);

            query = query.Trim();
            TranslationMapping translationMapping = null;
            if (_alphabet is not null && !_alphabet.CanBeTranslated(query))
            {
                // We assume that if a query can be translated (containing characters of a language, like Chinese)
                // it actually means user doesn't want it to be translated to English letters.
                (stringToCompare, translationMapping) = _alphabet.Translate(stringToCompare);
            }

            var currentAcronymQueryIndex = 0;
            var acronymMatchData = new List<int>();
            int acronymsTotalCount = 0;
            int acronymsMatched = 0;

            var fullStringToCompareWithoutCase = opt.IgnoreCase ? stringToCompare.ToLower() : stringToCompare;
            var queryWithoutCase = opt.IgnoreCase ? query.ToLower() : query;

            var querySubstrings = queryWithoutCase.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int currentQuerySubstringIndex = 0;
            var currentQuerySubstring = querySubstrings[currentQuerySubstringIndex];
            var currentQuerySubstringCharacterIndex = 0;

            var firstMatchIndex = -1;
            var firstMatchIndexInWord = -1;
            var lastMatchIndex = 0;
            bool allQuerySubstringsMatched = false;
            bool matchFoundInPreviousLoop = false;
            bool allSubstringsContainedInCompareString = true;

            var indexList = new List<int>();
            List<int> spaceIndices = new List<int>();

            for (var compareStringIndex = 0; compareStringIndex < fullStringToCompareWithoutCase.Length; compareStringIndex++)
            {
                // If acronyms matching successfully finished, this gets the remaining not matched acronyms for score calculation
                if (currentAcronymQueryIndex >= query.Length && acronymsMatched == query.Length)
                {
                    if (IsAcronymCount(stringToCompare, compareStringIndex))
                        acronymsTotalCount++;
                    continue;
                }

                if (currentAcronymQueryIndex >= query.Length ||
                    currentAcronymQueryIndex >= query.Length && allQuerySubstringsMatched)
                    break;

                // To maintain a list of indices which correspond to spaces in the string to compare
                // To populate the list only for the first query substring
                if (fullStringToCompareWithoutCase[compareStringIndex] == ' ' && currentQuerySubstringIndex == 0)
                    spaceIndices.Add(compareStringIndex);

                // Acronym Match
                if (IsAcronym(stringToCompare, compareStringIndex))
                {
                    if (fullStringToCompareWithoutCase[compareStringIndex] ==
                        queryWithoutCase[currentAcronymQueryIndex])
                    {
                        acronymMatchData.Add(compareStringIndex);
                        acronymsMatched++;

                        currentAcronymQueryIndex++;
                    }
                }

                if (IsAcronymCount(stringToCompare, compareStringIndex))
                    acronymsTotalCount++;

                if (allQuerySubstringsMatched || fullStringToCompareWithoutCase[compareStringIndex] !=
                    currentQuerySubstring[currentQuerySubstringCharacterIndex])
                {
                    matchFoundInPreviousLoop = false;

                    continue;
                }

                if (firstMatchIndex < 0)
                {
                    // first matched char will become the start of the compared string
                    firstMatchIndex = compareStringIndex;
                }

                if (currentQuerySubstringCharacterIndex == 0)
                {
                    // first letter of current word
                    matchFoundInPreviousLoop = true;
                    firstMatchIndexInWord = compareStringIndex;
                }
                else if (!matchFoundInPreviousLoop)
                {
                    // we want to verify that there is not a better match if this is not a full word
                    // in order to do so we need to verify all previous chars are part of the pattern
                    var startIndexToVerify = compareStringIndex - currentQuerySubstringCharacterIndex;

                    if (AllPreviousCharsMatched(startIndexToVerify, currentQuerySubstringCharacterIndex,
                        fullStringToCompareWithoutCase, currentQuerySubstring))
                    {
                        matchFoundInPreviousLoop = true;

                        // if it's the beginning character of the first query substring that is matched then we need to update start index
                        firstMatchIndex = currentQuerySubstringIndex == 0 ? startIndexToVerify : firstMatchIndex;

                        indexList = GetUpdatedIndexList(startIndexToVerify, currentQuerySubstringCharacterIndex,
                            firstMatchIndexInWord, indexList);
                    }
                }

                lastMatchIndex = compareStringIndex + 1;
                indexList.Add(compareStringIndex);

                currentQuerySubstringCharacterIndex++;

                // if finished looping through every character in the current substring
                if (currentQuerySubstringCharacterIndex == currentQuerySubstring.Length)
                {
                    // if any of the substrings was not matched then consider as all are not matched
                    allSubstringsContainedInCompareString =
                        matchFoundInPreviousLoop && allSubstringsContainedInCompareString;

                    currentQuerySubstringIndex++;

                    allQuerySubstringsMatched =
                        AllQuerySubstringsMatched(currentQuerySubstringIndex, querySubstrings.Length);

                    if (allQuerySubstringsMatched)
                        continue;

                    // otherwise move to the next query substring
                    currentQuerySubstring = querySubstrings[currentQuerySubstringIndex];
                    currentQuerySubstringCharacterIndex = 0;
                }
            }

            // return acronym match if all query char matched
            if (acronymsMatched > 0 && acronymsMatched == query.Length)
            {
                int acronymScore = acronymsMatched * 100 / acronymsTotalCount;

                if (acronymScore >= (int)UserSettingSearchPrecision)
                {
                    acronymMatchData = acronymMatchData.Select(x => translationMapping?.MapToOriginalIndex(x) ?? x).Distinct().ToList();
                    return new MatchResult(true, UserSettingSearchPrecision, acronymMatchData, acronymScore);
                }
            }

            // proceed to calculate score if every char or substring without whitespaces matched
            if (allQuerySubstringsMatched)
            {
                var nearestSpaceIndex = CalculateClosestSpaceIndex(spaceIndices, firstMatchIndex);

                // firstMatchIndex - nearestSpaceIndex - 1 is to set the firstIndex as the index of the first matched char
                // preceded by a space e.g. 'world' matching 'hello world' firstIndex would be 0 not 6 
                // giving more weight than 'we or donald' by allowing the distance calculation to treat the starting position at after the space.
                var score = CalculateSearchScore(query, stringToCompare, firstMatchIndex - nearestSpaceIndex - 1, spaceIndices,
                    lastMatchIndex - firstMatchIndex, allSubstringsContainedInCompareString);

                var resultList = indexList.Select(x => translationMapping?.MapToOriginalIndex(x) ?? x).Distinct().ToList();
                return new MatchResult(true, UserSettingSearchPrecision, resultList, score);
            }

            return new MatchResult(false, UserSettingSearchPrecision);
        }

        private bool IsAcronym(string stringToCompare, int compareStringIndex)
        {
            if (IsAcronymChar(stringToCompare, compareStringIndex) || IsAcronymNumber(stringToCompare, compareStringIndex))
                return true;

            return false;
        }

        // When counting acronyms, treat a set of numbers as one acronym ie. Visual 2019 as 2 acronyms instead of 5
        private bool IsAcronymCount(string stringToCompare, int compareStringIndex)
        {
            if (IsAcronymChar(stringToCompare, compareStringIndex))
                return true;

            if (IsAcronymNumber(stringToCompare, compareStringIndex))
                return compareStringIndex == 0 || char.IsWhiteSpace(stringToCompare[compareStringIndex - 1]);

            return false;
        }

        private bool IsAcronymChar(string stringToCompare, int compareStringIndex)
            => char.IsUpper(stringToCompare[compareStringIndex]) ||
               compareStringIndex == 0 || // 0 index means char is the start of the compare string, which is an acronym
               char.IsWhiteSpace(stringToCompare[compareStringIndex - 1]);

        private bool IsAcronymNumber(string stringToCompare, int compareStringIndex)
            => stringToCompare[compareStringIndex] >= 0 && stringToCompare[compareStringIndex] <= 9;

        // To get the index of the closest space which preceeds the first matching index
        private int CalculateClosestSpaceIndex(List<int> spaceIndices, int firstMatchIndex)
        {
            var closestSpaceIndex = -1;

            // spaceIndices should be ordered asc
            foreach (var index in spaceIndices)
            {
                if (index < firstMatchIndex)
                    closestSpaceIndex = index;
                else
                    break;
            }

            return closestSpaceIndex;
        }

        private static bool AllPreviousCharsMatched(int startIndexToVerify, int currentQuerySubstringCharacterIndex,
            string fullStringToCompareWithoutCase, string currentQuerySubstring)
        {
            var allMatch = true;
            for (int indexToCheck = 0; indexToCheck < currentQuerySubstringCharacterIndex; indexToCheck++)
            {
                if (fullStringToCompareWithoutCase[startIndexToVerify + indexToCheck] !=
                    currentQuerySubstring[indexToCheck])
                {
                    allMatch = false;
                }
            }

            return allMatch;
        }

        private static List<int> GetUpdatedIndexList(int startIndexToVerify, int currentQuerySubstringCharacterIndex,
            int firstMatchIndexInWord, List<int> indexList)
        {
            var updatedList = new List<int>();

            indexList.RemoveAll(x => x >= firstMatchIndexInWord);

            updatedList.AddRange(indexList);

            for (int indexToCheck = 0; indexToCheck < currentQuerySubstringCharacterIndex; indexToCheck++)
            {
                updatedList.Add(startIndexToVerify + indexToCheck);
            }

            return updatedList;
        }

        private static bool AllQuerySubstringsMatched(int currentQuerySubstringIndex, int querySubstringsLength)
        {
            // Acronym won't utilize the substring to match
            return currentQuerySubstringIndex >= querySubstringsLength;
        }

        private static int CalculateSearchScore(string query, string stringToCompare, int firstIndex, List<int> spaceIndices, int matchLen,
            bool allSubstringsContainedInCompareString)
        {
            // A match found near the beginning of a string is scored more than a match found near the end
            // A match is scored more if the characters in the patterns are closer to each other, 
            // while the score is lower if they are more spread out
            var score = 100 * (query.Length + 1) / ((1 + firstIndex) + (matchLen + 1));

            // Give more weight to a match that is closer to the start of the string. 
            // if the first matched char is immediately before space and all strings are contained in the compare string e.g. 'world' matching 'hello world'
            // and 'world hello', because both have 'world' immediately preceded by space, their firstIndex will be 0 when distance is calculated,
            // to prevent them scoring the same, we adjust the score by deducting the number of spaces it has from the start of the string, so 'world hello'
            // will score slightly higher than 'hello world' because 'hello world' has one additional space.
            if (firstIndex == 0 && allSubstringsContainedInCompareString)
                score -= spaceIndices.Count;

            // A match with less characters assigning more weights
            if (stringToCompare.Length - query.Length < 5)
            {
                score += 20;
            }
            else if (stringToCompare.Length - query.Length < 10)
            {
                score += 10;
            }

            if (allSubstringsContainedInCompareString)
            {
                int count = query.Count(c => !char.IsWhiteSpace(c));
                //10 per char is too much for long query strings, this threshhold is to avoid where long strings will override the other results too much
                int threshold = 4;
                if (count <= threshold)
                {
                    score += count * 10;
                }
                else
                {
                    score += threshold * 10 + (count - threshold) * 5;
                }
            }

            return score;
        }
    }

    public class MatchOption
    {
        public bool IgnoreCase { get; set; } = true;
    }
}
