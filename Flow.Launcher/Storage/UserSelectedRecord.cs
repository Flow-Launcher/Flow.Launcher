using System;
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
                // https://stackoverflow.com/a/5155015 31 prime and is 2^5 - 1 which allows fast
                //    optimization without information lost when int overflow
                
                for (int i = 0; i < query.ActionKeyword.Length; i++)
                {
                    char item = query.ActionKeyword[i];
                    hashcode = hashcode * 31 + item;
                }
                
                for (int i = 0; i < query.Search.Length; i++)
                {
                    char item = query.Search[i];
                    hashcode = hashcode * 31 + item;
                }

                for (int i = 0; i < result.Title.Length; i++)
                {
                    char item = result.Title[i];
                    hashcode = hashcode * 31 + item;
                }

                for (int i = 0; i < result.SubTitle.Length; i++)
                {
                    char item = result.SubTitle[i];
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
