﻿using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Infrastructure.Storage
{
    /// <summary>
    /// Serialize object using json format.
    /// </summary>
    public class JsonStrorage<T>
    {
        private readonly JsonSerializerOptions _serializerSettings;
        private T _data;
        // need a new directory name
        public const string DirectoryName = "Settings";
        public const string FileSuffix = ".json";
        public string FilePath { get; set; }
        public string DirectoryPath { get; set; }


        internal JsonStrorage()
        {
            // use property initialization instead of DefaultValueAttribute
            // easier and flexible for default value of object
            _serializerSettings = new JsonSerializerOptions
            {
                IgnoreNullValues = false
            };
        }

        public T Load()
        {
            if (File.Exists(FilePath))
            {
                var searlized = File.ReadAllText(FilePath);
                if (!string.IsNullOrWhiteSpace(searlized))
                {
                    Deserialize(searlized);
                }
                else
                {
                    LoadDefault();
                }
            }
            else
            {
                LoadDefault();
            }
            return _data.NonNull();
        }

        private void Deserialize(string searlized)
        {
            try
            {
                _data = JsonSerializer.Deserialize<T>(searlized, _serializerSettings);
            }
            catch (JsonException e)
            {
                LoadDefault();
                Log.Exception($"|JsonStrorage.Deserialize|Deserialize error for json <{FilePath}>", e);
            }

            if (_data == null)
            {
                LoadDefault();
            }
        }

        private void LoadDefault()
        {
            if (File.Exists(FilePath))
            {
                BackupOriginFile();
            }

            _data = JsonSerializer.Deserialize<T>("{}", _serializerSettings);
            Save();
        }

        private void BackupOriginFile()
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-fffffff", CultureInfo.CurrentUICulture);
            var directory = Path.GetDirectoryName(FilePath).NonNull();
            var originName = Path.GetFileNameWithoutExtension(FilePath);
            var backupName = $"{originName}-{timestamp}{FileSuffix}";
            var backupPath = Path.Combine(directory, backupName);
            File.Copy(FilePath, backupPath, true);
            // todo give user notification for the backup process
        }

        public void Save()
        {
            string serialized = JsonSerializer.Serialize(_data, new JsonSerializerOptions() { WriteIndented = true });

            File.WriteAllText(FilePath, serialized);
        }
    }
}
