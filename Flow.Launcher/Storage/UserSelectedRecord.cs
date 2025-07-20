using System.Collections.Generic;
using System.Text.Json.Serialization;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage
{
    public class UserSelectedRecord
    {
        private const int HASH_MULTIPLIER = 31;
        private const int HASH_INITIAL = 23;

        [JsonInclude]
        public Dictionary<int, int> recordsWithQuery { get; private set; }

        [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, int> records { get; private set; }

        public UserSelectedRecord()
        {
            recordsWithQuery = new Dictionary<int, int>();
        }

        private static int GenerateStaticHashCode(string s, int start = HASH_INITIAL)
        {
            if (s == null)
            {
                return start;
            }

            unchecked
            {
                // skip the empty space
                // https://stackoverflow.com/a/5155015 31 prime and is 2^5 - 1 which allows fast
                //    optimization without information lost when int overflow

                for (int i = 0; i < s.Length; i++)
                {
                    start = start * HASH_MULTIPLIER + s[i];
                }

                return start;
            }
        }

        private static int GenerateResultHashCode(Result result)
        {
            if (string.IsNullOrEmpty(result.RecordKey))
            {
                int hashcode = GenerateStaticHashCode(result.Title);
                return GenerateStaticHashCode(result.SubTitle, hashcode);
            }
            else
            {
                return GenerateStaticHashCode(result.RecordKey);
            }
        }

        private static int GenerateQueryAndResultHashCode(Query query, Result result)
        {
            // query is null when user select the context menu item directly of one item from query list
            // so we only need to consider the result
            if (query == null)
            {
                return GenerateResultHashCode(result);
            }

            int hashcode = GenerateStaticHashCode(query.ActionKeyword);
            hashcode = GenerateStaticHashCode(query.Search, hashcode);

            if (string.IsNullOrEmpty(result.RecordKey))
            {
                hashcode = GenerateStaticHashCode(result.Title, hashcode);
                hashcode = GenerateStaticHashCode(result.SubTitle, hashcode);
            }
            else
            {
                hashcode = GenerateStaticHashCode(result.RecordKey, hashcode);
            }

            return hashcode;
        }

        private void TransformOldRecords()
        {
            if (records != null)
            {
                var localRecords = records;
                records = null;

                foreach (var pair in localRecords)
                {
                    recordsWithQuery.TryAdd(GenerateStaticHashCode(pair.Key), pair.Value);
                }
            }
        }

        public void Add(Result result)
        {
            TransformOldRecords();

            var keyWithQuery = GenerateQueryAndResultHashCode(result.OriginQuery, result);

            if (!recordsWithQuery.TryAdd(keyWithQuery, 1))
                recordsWithQuery[keyWithQuery]++;

            var keyWithoutQuery = GenerateResultHashCode(result);

            if (!recordsWithQuery.TryAdd(keyWithoutQuery, 1))
                recordsWithQuery[keyWithoutQuery]++;
        }

        public int GetSelectedCount(Result result)
        {
            var selectedCount = 0;

            recordsWithQuery.TryGetValue(GenerateQueryAndResultHashCode(result.OriginQuery, result), out int value);
            selectedCount += value * 5;

            recordsWithQuery.TryGetValue(GenerateResultHashCode(result), out value);
            selectedCount += value;

            return selectedCount;
        }
    }
}
