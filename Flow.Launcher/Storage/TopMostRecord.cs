using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage
{
    public class TopMostRecord
    {
        [JsonInclude]
        public ConcurrentDictionary<string, Record> records { get; private set; } = new ConcurrentDictionary<string, Record>();

        internal bool IsTopMost(Result result)
        {
            if (records.IsEmpty || result.OriginQuery == null ||
                !records.TryGetValue(result.OriginQuery.RawQuery, out var value))
            {
                return false;
            }

            // since this dictionary should be very small (or empty) going over it should be pretty fast.
            return value.Equals(result);
        }

        internal void Remove(Result result)
        {
            records.Remove(result.OriginQuery.RawQuery, out _);
        }

        internal void AddOrUpdate(Result result)
        {
            var record = new Record
            {
                PluginID = result.PluginID,
                Title = result.Title,
                SubTitle = result.SubTitle
            };
            records.AddOrUpdate(result.OriginQuery.RawQuery, record, (key, oldValue) => record);
        }

        public void Load(Dictionary<string, Record> dictionary)
        {
            records = new ConcurrentDictionary<string, Record>(dictionary);
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
