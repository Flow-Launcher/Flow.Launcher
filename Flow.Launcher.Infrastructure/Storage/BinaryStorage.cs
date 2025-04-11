﻿using System.IO;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;
using MemoryPack;

#nullable enable

namespace Flow.Launcher.Infrastructure.Storage
{
    /// <summary>
    /// Stroage object using binary data
    /// Normally, it has better performance, but not readable
    /// </summary>
    /// <remarks>
    /// It utilizes MemoryPack, which means the object must be MemoryPackSerializable <see href="https://github.com/Cysharp/MemoryPack"/>
    /// </remarks>
    public class BinaryStorage<T> : ISavable
    {
        protected T? Data;

        public const string FileSuffix = ".cache";

        protected string FilePath { get; init; } = null!;

        protected string DirectoryPath { get; init; } = null!;

        // Let the derived class to set the file path
        protected BinaryStorage()
        {
        }

        public BinaryStorage(string filename)
        {
            DirectoryPath = DataLocation.CacheDirectory;
            FilesFolders.ValidateDirectory(DirectoryPath);

            FilePath = Path.Combine(DirectoryPath, $"{filename}{FileSuffix}");
        }

        public async ValueTask<T> TryLoadAsync(T defaultData)
        {
            if (Data != null) return Data;

            if (File.Exists(FilePath))
            {
                if (new FileInfo(FilePath).Length == 0)
                {
                    Log.Error($"|BinaryStorage.TryLoad|Zero length cache file <{FilePath}>");
                    Data = defaultData;
                    await SaveAsync();
                }

                await using var stream = new FileStream(FilePath, FileMode.Open);
                Data = await DeserializeAsync(stream, defaultData);
            }
            else
            {
                Log.Info("|BinaryStorage.TryLoad|Cache file not exist, load default data");
                Data = defaultData;
                await SaveAsync();
            }

            return Data;
        }

        private static async ValueTask<T> DeserializeAsync(Stream stream, T defaultData)
        {
            try
            {
                var t = await MemoryPackSerializer.DeserializeAsync<T>(stream);
                return t ?? defaultData;
            }
            catch (System.Exception)
            {
                // Log.Exception($"|BinaryStorage.Deserialize|Deserialize error for file <{FilePath}>", e);
                return defaultData;
            }
        }

        public void Save()
        {
            // User may delete the directory, so we need to check it
            FilesFolders.ValidateDirectory(DirectoryPath);

            var serialized = MemoryPackSerializer.Serialize(Data);
            File.WriteAllBytes(FilePath, serialized);
        }

        public async ValueTask SaveAsync()
        {
            await SaveAsync(Data.NonNull());
        }

        // ImageCache need to convert data into concurrent dictionary for usage,
        // so we would better to clear the data
        public void ClearData()
        {
            Data = default;
        }

        // ImageCache storages data in its class,
        // so we need to pass it to SaveAsync
        public async ValueTask SaveAsync(T data)
        {
            // User may delete the directory, so we need to check it
            FilesFolders.ValidateDirectory(DirectoryPath);

            await using var stream = new FileStream(FilePath, FileMode.Create);
            await MemoryPackSerializer.SerializeAsync(stream, data);
        }
    }
}
