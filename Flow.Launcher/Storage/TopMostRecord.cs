using System;
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

        /// <summary>
        /// Initializes the top most records storage, handling migration from the old single-record-per-query format to the new multiple-records-per-query format if necessary.
        /// </summary>
        /// <remarks>
        /// If new-format data exists, it loads it and deletes any old-format data. If only old-format data exists, it migrates the data to the new format, deletes the old data, and saves the new structure. If neither exists, it initializes an empty new-format storage.
        /// </remarks>
        public FlowLauncherJsonStorageTopMostRecord()
        {
            // Get old data & new data
            var topMostRecordStorage = new FlowLauncherJsonStorage<TopMostRecord>();
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
                _topMostRecord.Add(topMostRecordStorage.Load());

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

        /// <summary>
        /// Persists the current top most records to storage.
        /// </summary>
        public void Save()
        {
            _topMostRecordStorage.Save();
        }

        /// <summary>
        /// Determines whether the specified result is marked as top most in the current records.
        /// </summary>
        /// <param name="result">The result to check.</param>
        /// <returns>True if the result is marked as top most; otherwise, false.</returns>
        public bool IsTopMost(Result result)
        {
            return _topMostRecord.IsTopMost(result);
        }

        /// <summary>
        /// Removes the specified result from the top most records if it exists.
        /// </summary>
        public void Remove(Result result)
        {
            _topMostRecord.Remove(result);
        }

        /// <summary>
        /// Adds a result to the top most records or updates it if it already exists.
        /// </summary>
        public void AddOrUpdate(Result result)
        {
            _topMostRecord.AddOrUpdate(result);
        }
    }

    /// <summary>
    /// Old data structure to support only one top most record for the same query
    /// </summary>
    internal class TopMostRecord
    {
        [JsonInclude]
        public ConcurrentDictionary<string, Record> records { get; private set; } = new();

        /// <summary>
        /// Determines whether the specified result is the top most record for its originating query.
        /// </summary>
        /// <param name="result">The result to check.</param>
        /// <returns>True if the result matches the stored top most record for its query; otherwise, false.</returns>
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

        /// <summary>
        /// Adds or updates the top most record for the specified result's query, replacing any existing record for that query.
        /// </summary>
        /// <param name="result">The result whose information is to be stored as the top most record for its originating query. If <c>OriginQuery</c> is null, no action is taken.</param>
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

        /// <summary>
        /// Migrates all records from an existing <see cref="TopMostRecord"/> into this multiple-records-per-query structure.
        /// </summary>
        /// <param name="topMostRecord">The old single-record-per-query data structure to migrate from.</param>
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

        /// <summary>
        /// Determines whether the specified result is marked as top most for its originating query.
        /// </summary>
        /// <param name="result">The result to check.</param>
        /// <returns>True if the result is a top most record for its query; otherwise, false.</returns>
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

        /// <summary>
        /// Removes a matching record for the given result from the collection of top most records for its query.
        /// </summary>
        /// <param name="result">The result whose corresponding record should be removed.</param>
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

            // if the bag is empty, remove the bag from the dictionary
            if (value.IsEmpty)
            {
                records.TryRemove(result.OriginQuery.RawQuery, out _);
            }
        }

        /// <summary>
        /// Adds a result as a top most record for its originating query, or updates the existing record if it already exists.
        /// </summary>
        /// <param name="result">The result to add or update as top most for its query. Ignored if the result's OriginQuery is null.</param>
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

    /// <summary>
    /// Because ConcurrentBag does not support serialization, we need to convert it to a List
    /// </summary>
    internal class ConcurrentDictionaryConcurrentBagConverter : JsonConverter<ConcurrentDictionary<string, ConcurrentBag<Record>>>
    {
        /// <summary>
        /// Deserializes JSON into a <see cref="ConcurrentDictionary{TKey, TValue}"/> mapping strings to <see cref="ConcurrentBag{T}"/> of <see cref="Record"/>.
        /// </summary>
        /// <param name="reader">The JSON reader positioned at the start of the object.</param>
        /// <param name="typeToConvert">The type to convert (ignored).</param>
        /// <param name="options">Serialization options to use during deserialization.</param>
        /// <returns>A concurrent dictionary where each key maps to a concurrent bag of records.</returns>
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

        /// <summary>
        /// Serializes a <see cref="ConcurrentDictionary{TKey, TValue}"/> of <see cref="ConcurrentBag{T}"/> records to JSON by converting each bag to a list.
        /// </summary>
        /// <param name="writer">The JSON writer to which the data will be serialized.</param>
        /// <param name="value">The concurrent dictionary containing bags of records to serialize.</param>
        /// <param name="options">Serialization options to use.</param>
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

        /// <summary>
        /// Determines whether the current record is equal to the specified result based on key or identifying properties.
        /// </summary>
        /// <param name="r">The result to compare with this record.</param>
        /// <returns>True if the records are considered equal; otherwise, false.</returns>
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
