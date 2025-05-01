using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage
{
    public class FlowLauncherJsonStorageTopMostRecord : ISavable
    {
        private readonly FlowLauncherJsonStorage<MultipleTopMostRecord> _topMostRecordStorage;
        private readonly MultipleTopMostRecord _topMostRecord;

        public FlowLauncherJsonStorageTopMostRecord()
        {
            var topMostRecordStorage = new FlowLauncherJsonStorage<TopMostRecord>();
            var exist = topMostRecordStorage.Exists();
            if (exist)
            {
                // Get old data
                var topMostRecord = topMostRecordStorage.Load();

                // Convert to new data
                _topMostRecordStorage = new FlowLauncherJsonStorage<MultipleTopMostRecord>();
                _topMostRecord = _topMostRecordStorage.Load();
                _topMostRecord.Add(topMostRecord);
            }
            else
            {
                // Get new data
                _topMostRecordStorage = new FlowLauncherJsonStorage<MultipleTopMostRecord>();
                _topMostRecord = _topMostRecordStorage.Load();
            }
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
        public ConcurrentDictionary<string, Record> records { get; private set; } = new();

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

    public class MultipleTopMostRecord
    {
        [JsonInclude]
        public ConcurrentDictionary<string, ConcurrentBag<Record>> records { get; private set; } = new();

        internal void Add(TopMostRecord topMostRecord)
        {
            if (topMostRecord == null || topMostRecord.records.IsEmpty)
            {
                return;
            }

            foreach (var record in topMostRecord.records)
            {
                records.AddOrUpdate(record.Key, new ConcurrentBag<Record> { record.Value }, (key, oldValue) =>
                {
                    oldValue.Add(record.Value);
                    return oldValue;
                });
            }
        }

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
            return value.Any(record => record.Equals(result));
        }

        internal void Remove(Result result)
        {
            // origin query is null when user select the context menu item directly of one item from query list
            // in this case, we do not need to remove the record
            if (result.OriginQuery == null ||
                !records.TryGetValue(result.OriginQuery.RawQuery, out var value))
            {
                return;
            }

            // remove the record from the bag
            var recordToRemove = value.FirstOrDefault(r => r.Equals(result));
            if (recordToRemove != null)
            {
                value.TryTake(out recordToRemove);
            }
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
            if (!records.TryGetValue(result.OriginQuery.RawQuery, out var value))
            {
                // create a new bag if it does not exist
                value = new ConcurrentBag<Record>()
                {
                    record
                };
                records.TryAdd(result.OriginQuery.RawQuery, value);
            }
            else
            {
                // add or update the record in the bag
                if (value.Any(r => r.Equals(result)))
                {
                    // update the record
                    var recordToUpdate = value.FirstOrDefault(r => r.Equals(result));
                    if (recordToUpdate != null)
                    {
                        value.TryTake(out recordToUpdate);
                        value.Add(record);
                    }
                }
                else
                {
                    // add the record
                    value.Add(record);
                }
            }
        }
    }

    public class Record
    {
        public string Title { get; init; }
        public string SubTitle { get; init; }
        public string PluginID { get; init; }
        public string RecordKey { get; init; }

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
