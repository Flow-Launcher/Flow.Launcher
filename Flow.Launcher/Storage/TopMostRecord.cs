﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage
{
    public class FlowLauncherJsonStorageTopMostRecord
    {
        private readonly FlowLauncherJsonStorage<MultipleTopMostRecord> _topMostRecordStorage;
        private readonly MultipleTopMostRecord _topMostRecord;

        public FlowLauncherJsonStorageTopMostRecord()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            // Get old data & new data
            var topMostRecordStorage = new FlowLauncherJsonStorage<TopMostRecord>();
#pragma warning restore CS0618 // Type or member is obsolete
            _topMostRecordStorage = new FlowLauncherJsonStorage<MultipleTopMostRecord>();

            // Check if data exist
            var oldDataExist = topMostRecordStorage.Exists();
            var newDataExist = _topMostRecordStorage.Exists();

            // If new data exist, it means we have already migrated the old data
            // So we can safely delete the old data and load the new data
            if (newDataExist)
            {
                try
                {
                    topMostRecordStorage.Delete();
                }
                catch
                {
                    // Ignored - Flow will delete the old data during next startup
                }
                _topMostRecord = _topMostRecordStorage.Load();
            }
            // If new data does not exist and old data exist, we need to migrate the old data to the new data
            else if (oldDataExist)
            {
                // Migrate old data to new data
                _topMostRecord = _topMostRecordStorage.Load();
                var oldTopMostRecord = topMostRecordStorage.Load();
                if (oldTopMostRecord == null || oldTopMostRecord.records.IsEmpty) return;
                foreach (var record in oldTopMostRecord.records)
                {
                    _topMostRecord.records.AddOrUpdate(record.Key, new ConcurrentBag<Record> { record.Value }, (key, oldValue) =>
                    {
                        oldValue.Add(record.Value);
                        return oldValue;
                    });
                }

                // Delete old data and save the new data
                try
                {
                    topMostRecordStorage.Delete();
                }
                catch
                {
                    // Ignored - Flow will delete the old data during next startup
                }
                Save();
            }
            // If both data do not exist, we just need to create a new data
            else
            {
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

    /// <summary>
    /// Old data structure to support only one top most record for the same query
    /// </summary>
    [Obsolete("Use MultipleTopMostRecord instead. This class will be removed in future versions.")]
    internal class TopMostRecord
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

    /// <summary>
    /// New data structure to support multiple top most records for the same query
    /// </summary>
    internal class MultipleTopMostRecord
    {
        [JsonInclude]
        [JsonConverter(typeof(ConcurrentDictionaryConcurrentBagConverter))]
        public ConcurrentDictionary<string, ConcurrentBag<Record>> records { get; private set; } = new();

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
            var bag = new ConcurrentQueue<Record>(value.Where(r => !r.Equals(result)));
            if (bag.IsEmpty)
            {
                // if the bag is empty, remove the bag from the dictionary
                records.TryRemove(result.OriginQuery.RawQuery, out _);
            }
            else
            {
                // change the bag in the dictionary
                records[result.OriginQuery.RawQuery] = new ConcurrentBag<Record>(bag);
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
                var bag = new ConcurrentQueue<Record>(value.Where(r => !r.Equals(result))); // make sure we don't have duplicates
                bag.Enqueue(record);
                records[result.OriginQuery.RawQuery] = new ConcurrentBag<Record>(bag);
            }
        }
    }

    /// <summary>
    /// Because ConcurrentBag does not support serialization, we need to convert it to a List
    /// </summary>
    internal class ConcurrentDictionaryConcurrentBagConverter : JsonConverter<ConcurrentDictionary<string, ConcurrentBag<Record>>>
    {
        public override ConcurrentDictionary<string, ConcurrentBag<Record>> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var dictionary = JsonSerializer.Deserialize<Dictionary<string, List<Record>>>(ref reader, options);
            var concurrentDictionary = new ConcurrentDictionary<string, ConcurrentBag<Record>>();
            foreach (var kvp in dictionary)
            {
                concurrentDictionary.TryAdd(kvp.Key, new ConcurrentBag<Record>(kvp.Value));
            }
            return concurrentDictionary;
        }

        public override void Write(Utf8JsonWriter writer, ConcurrentDictionary<string, ConcurrentBag<Record>> value, JsonSerializerOptions options)
        {
            var dict = new Dictionary<string, List<Record>>();
            foreach (var kvp in value)
            {
                dict.Add(kvp.Key, kvp.Value.ToList());
            }
            JsonSerializer.Serialize(writer, dict, options);
        }
    }

    internal class Record
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
