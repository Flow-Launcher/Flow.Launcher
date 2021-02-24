using System.Collections.Generic;
using System.Text.Json.Serialization;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage
{
    public class UserSelectedRecord
    {
        [JsonInclude]
        public Dictionary<string, int> records { get; private set; }

        public UserSelectedRecord()
        {
            records = new Dictionary<string, int>();
        }

        public void Add(Result result)
        {
            var key = result.ToString();
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
            if (records.TryGetValue(result.ToString(), out int value))
            {
                return value;
            }
            return 0;
        }
    }
}
