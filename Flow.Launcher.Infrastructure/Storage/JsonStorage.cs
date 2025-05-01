using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;

#nullable enable

namespace Flow.Launcher.Infrastructure.Storage
{
    /// <summary>
    /// Serialize object using json format.
    /// </summary>
    public class JsonStorage<T> : ISavable where T : new()
    {
        private static readonly string ClassName = "JsonStorage";

        protected T? Data;

        // need a new directory name
        public const string DirectoryName = Constant.Settings;
        public const string FileSuffix = ".json";

        protected string FilePath { get; init; } = null!;

        private string TempFilePath => $"{FilePath}.tmp";

        private string BackupFilePath => $"{FilePath}.bak";

        protected string DirectoryPath { get; init; } = null!;

        // Let the derived class to set the file path
        protected JsonStorage()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <c>JsonStorage&lt;T&gt;</c> class for the specified file path.
        /// </summary>
        /// <param name="filePath">The full path to the JSON file used for storage.</param>
        /// <exception cref="ArgumentException">Thrown if the provided file path does not contain a valid directory.</exception>
        public JsonStorage(string filePath)
        {
            FilePath = filePath;
            DirectoryPath = Path.GetDirectoryName(filePath) ?? throw new ArgumentException("Invalid file path");

            FilesFolders.ValidateDirectory(DirectoryPath);
        }

        /// <summary>
        /// Determines whether the main JSON storage file exists on disk.
        /// </summary>
        /// <returns>True if the storage file exists; otherwise, false.</returns>
        public bool Exists()
        {
            return File.Exists(FilePath);
        }

        /// <summary>
        /// Deletes the main JSON storage file, its backup, and temporary file if they exist.
        /// </summary>
        public void Delete()
        {
            foreach (var path in new[] { FilePath, BackupFilePath, TempFilePath })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        /// <summary>
        /// Asynchronously loads and deserializes the JSON file into <c>Data</c>, falling back to a backup or a default instance if necessary.
        /// </summary>
        /// <returns>The loaded or newly created instance of <typeparamref name="T"/>.</returns>
        public async Task<T> LoadAsync()
        {
            if (Data != null)
                return Data;

            string? serialized = null;

            if (File.Exists(FilePath))
            {
                serialized = await File.ReadAllTextAsync(FilePath);
            }

            if (!string.IsNullOrEmpty(serialized))
            {
                try
                {
                    Data = JsonSerializer.Deserialize<T>(serialized) ?? await LoadBackupOrDefaultAsync();
                }
                catch (JsonException)
                {
                    Data = await LoadBackupOrDefaultAsync();
                }
            }
            else
            {
                Data = await LoadBackupOrDefaultAsync();
            }

            return Data.NonNull();
        }

        private async ValueTask<T> LoadBackupOrDefaultAsync()
        {
            var backup = await TryLoadBackupAsync();

            return backup ?? LoadDefault();
        }

        private async ValueTask<T?> TryLoadBackupAsync()
        {
            if (!File.Exists(BackupFilePath))
                return default;

            try
            {
                await using var source = File.OpenRead(BackupFilePath);
                var data = await JsonSerializer.DeserializeAsync<T>(source) ?? default;

                if (data != null)
                    RestoreBackup();

                return data;
            }
            catch (JsonException)
            {
                return default;
            }
        }

        private void RestoreBackup()
        {
            Log.Info(ClassName, $"Failed to load settings.json, {BackupFilePath} restored successfully");

            if (File.Exists(FilePath))
                File.Replace(BackupFilePath, FilePath, null);
            else
                File.Move(BackupFilePath, FilePath);
        }

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
                    Data = JsonSerializer.Deserialize<T>(serialized) ?? TryLoadBackup() ?? LoadDefault();
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
                    RestoreBackup();

                return data;
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
            // User may delete the directory, so we need to check it
            FilesFolders.ValidateDirectory(DirectoryPath);

            var serialized = JsonSerializer.Serialize(Data,
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(TempFilePath, serialized);

            AtomicWriteSetting();
        }

        public async Task SaveAsync()
        {
            // User may delete the directory, so we need to check it
            FilesFolders.ValidateDirectory(DirectoryPath);

            await using var tempOutput = File.OpenWrite(TempFilePath);
            await JsonSerializer.SerializeAsync(tempOutput, Data,
                new JsonSerializerOptions { WriteIndented = true });
            AtomicWriteSetting();
        }

        private void AtomicWriteSetting()
        {
            if (!File.Exists(FilePath))
            {
                File.Move(TempFilePath, FilePath);
            }
            else
            {
                var finalFilePath = new FileInfo(FilePath).LinkTarget ?? FilePath;
                File.Replace(TempFilePath, finalFilePath, BackupFilePath);
            }
        }
    }
}
