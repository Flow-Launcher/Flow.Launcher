using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Infrastructure.Storage
{
    /// <summary>
    /// Serialize object using json format.
    /// </summary>
    public class JsonStorage<T> where T : new()
    {
        protected T _data;
        // need a new directory name
        public const string DirectoryName = "Settings";
        public const string FileSuffix = ".json";
        public string FilePath { get; set; }
        public string DirectoryPath { get; set; }


        public T Load()
        {
            if (File.Exists(FilePath))
            {
                var serialized = File.ReadAllText(FilePath);
                if (!string.IsNullOrWhiteSpace(serialized))
                {
                    Deserialize(serialized);
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

        private void Deserialize(string serialized)
        {
            try
            {
                _data = JsonSerializer.Deserialize<T>(serialized);
            }
            catch (JsonException e)
            {
                LoadDefault();
                Log.Exception($"|JsonStorage.Deserialize|Deserialize error for json <{FilePath}>", e);
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

            _data = new T();
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

    [Obsolete("Deprecated as of Flow Launcher v1.8.0, on 2021.06.21. " +
        "This is used only for Everything plugin v1.4.9 or below backwards compatibility")]
    public class JsonStrorage<T> : JsonStorage<T> where T : new() { }
}
