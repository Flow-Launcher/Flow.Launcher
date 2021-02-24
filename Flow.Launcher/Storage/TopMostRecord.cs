using System.Collections.Generic;
using System.Text.Json.Serialization;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage
{
    // todo this class is not thread safe.... but used from multiple threads.
    public class TopMostRecord
    {
        [JsonInclude]
        public Dictionary<string, Record> records { get; private set; } = new Dictionary<string, Record>();

        internal bool IsTopMost(Result result)
        {
            if (records.Count == 0 || !records.ContainsKey(result.OriginQuery.RawQuery))
            {
                return false;
            }

            // since this dictionary should be very small (or empty) going over it should be pretty fast.
            return records[result.OriginQuery.RawQuery].Equals(result);
        }

        internal void Remove(Result result)
        {
            records.Remove(result.OriginQuery.RawQuery);
        }

        internal void AddOrUpdate(Result result)
        {
            var record = new Record
            {
                PluginID = result.PluginID,
                Title = result.Title,
                SubTitle = result.SubTitle
            };
            records[result.OriginQuery.RawQuery] = record;

        }

        public void Load(Dictionary<string, Record> dictionary)
        {
            records = dictionary;
        }
    }


    public class Record
    {
        public string Title { get; set; }
        public string SubTitle { get; set; }
        public string PluginID { get; set; }

        public bool Equals(Result r)
        {
            return Title == r.Title
                && SubTitle == r.SubTitle
                && PluginID == r.PluginID;
        }
    }
}
