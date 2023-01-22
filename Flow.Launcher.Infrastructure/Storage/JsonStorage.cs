#nullable enable
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
        protected T? Data;
        // need a new directory name
        public const string DirectoryName = "Settings";
        public const string FileSuffix = ".json";
        protected string FilePath { get; init; } = null!;
        private string TempFilePath => $"{FilePath}.tmp";
        private string BackupFilePath => $"{FilePath}.bak";
        protected string DirectoryPath { get; init; } = null!;


        public T Load()
        {
            string? serialized = null;

            if (File.Exists(FilePath))
            {
                serialized = File.ReadAllText(FilePath);
            }

            if (!string.IsNullOrEmpty(serialized))
            {
                try
                {
                    Data = JsonSerializer.Deserialize<T>(serialized)?? TryLoadBackup() ?? LoadDefault();
                }
                catch (JsonException)
                {
                    Data = TryLoadBackup() ?? LoadDefault();
                }
            }
            else
            {
                Data = TryLoadBackup() ?? LoadDefault();
            }
            return Data.NonNull();
        }

        private T LoadDefault()
        {
            if (File.Exists(FilePath))
            {
                BackupOriginFile();
            }

            return new T();
        }

        private T? TryLoadBackup()
        {
            if (!File.Exists(BackupFilePath))
                return default;

            try
            {
                var data = JsonSerializer.Deserialize<T>(File.ReadAllText(BackupFilePath));
                if (data != null)
                {
                    Log.Info($"|JsonStorage.Load|Failed to load settings.json, {BackupFilePath} restored successfully");
                    File.Replace(BackupFilePath, FilePath, null);
                    return data;
                }
                return default;
            }
            catch (JsonException)
            {
                return default;
            }
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
            string serialized = JsonSerializer.Serialize(Data, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(TempFilePath, serialized);
            File.Replace(TempFilePath, FilePath, BackupFilePath);
            File.Delete(TempFilePath);
        }
    }
}
