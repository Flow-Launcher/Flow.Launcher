using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage
{
    public class FlowLauncherJsonStorageTopMostRecord : ISavable
    {
        private readonly FlowLauncherJsonStorage<TopMostRecord> _topMostRecordStorage;

        private readonly TopMostRecord _topMostRecord;

        public FlowLauncherJsonStorageTopMostRecord()
        {
            _topMostRecordStorage = new FlowLauncherJsonStorage<TopMostRecord>();
            _topMostRecord = _topMostRecordStorage.Load();
        }

        public void Save()
        {
            _topMostRecordStorage.Save();
        }

        public bool IsTopMost(Result result)
        {
            return _topMostRecord.IsTopMost(result);
        }

        public void Remove(Result result)
        {
            _topMostRecord.Remove(result);
        }

        public void AddOrUpdate(Result result)
        {
            _topMostRecord.AddOrUpdate(result);
        }
    }

    public class TopMostRecord
    {
        [JsonInclude]
        public ConcurrentDictionary<string, Record> records { get; private set; } = new ConcurrentDictionary<string, Record>();

        internal bool IsTopMost(Result result)
        {
            // origin query is null when user select the context menu item directly of one item from query list
            // in this case, we do not need to check if the result is top most
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
            // origin query is null when user select the context menu item directly of one item from query list
            // in this case, we do not need to remove the record
            if (result.OriginQuery == null)
            {
                return;
            }

            records.Remove(result.OriginQuery.RawQuery, out _);
        }

        internal void AddOrUpdate(Result result)
        {
            // origin query is null when user select the context menu item directly of one item from query list
            // in this case, we do not need to add or update the record
            if (result.OriginQuery == null)
            {
                return;
            }

            var record = new Record
            {
                PluginID = result.PluginID,
                Title = result.Title,
                SubTitle = result.SubTitle,
                RecordKey = result.RecordKey
            };
            records.AddOrUpdate(result.OriginQuery.RawQuery, record, (key, oldValue) => record);
        }
    }

    public class Record
    {
        public string Title { get; set; }
        public string SubTitle { get; set; }
        public string PluginID { get; set; }
        public string RecordKey { get; set; }

        public bool Equals(Result r)
        {
            if (string.IsNullOrEmpty(RecordKey) || string.IsNullOrEmpty(r.RecordKey))
            {
                return Title == r.Title
                    && SubTitle == r.SubTitle
                    && PluginID == r.PluginID;
            }
            else
            {
                return RecordKey == r.RecordKey
                    && PluginID == r.PluginID;
            }
        }
    }
}
