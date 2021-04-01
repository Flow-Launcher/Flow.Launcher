using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage
{
    public class UserSelectedRecord
    {
        [JsonInclude]
        public Dictionary<int, int> records { get; private set; }

        public UserSelectedRecord()
        {
            records = new Dictionary<int, int>();
        }

        private static int GenerateCustomHashCode(Query query, Result result)
        {
            int hashcode = 23;

            unchecked
            {
                // skip the empty space
                foreach (var item in query.ActionKeyword.Concat(query.Search))
                {
                    hashcode = hashcode * 31 + item;
                }

                foreach (var item in result.Title)
                {
                    hashcode = hashcode * 31 + item;
                }

                foreach (var item in result.SubTitle)
                {
                    hashcode = hashcode * 31 + item;
                }
                return hashcode;

            }
        }

        public void Add(Result result)
        {
            var key = GenerateCustomHashCode(result.OriginQuery, result);
            if (records.ContainsKey(key))
            {
                records[key]++;
            }
            else
            {
                records.Add(key, 1);

            }
        }

        public int GetSelectedCount(Result result)
        {
            if (records.TryGetValue(GenerateCustomHashCode(result.OriginQuery, result), out int value))
            {
                return value;
            }
            return 0;
        }
    }
}
