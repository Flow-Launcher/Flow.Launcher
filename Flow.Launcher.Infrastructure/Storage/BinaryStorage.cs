using System.IO;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using MemoryPack;

namespace Flow.Launcher.Infrastructure.Storage
{
    /// <summary>
    /// Stroage object using binary data
    /// Normally, it has better performance, but not readable
    /// </summary>
    /// <remarks>
    /// It utilize MemoryPack, which means the object must be MemoryPackSerializable <see href="https://github.com/Cysharp/MemoryPack"/>
    /// </remarks>
    public class BinaryStorage<T>
    {
        public const string FileSuffix = ".cache";

        // Let the derived class to set the file path
        public BinaryStorage(string filename, string directoryPath = null)
        {
            directoryPath ??= DataLocation.CacheDirectory;
            Helper.ValidateDirectory(directoryPath);

            FilePath = Path.Combine(directoryPath, $"{filename}{FileSuffix}");
        }

        public string FilePath { get; }

        public async ValueTask<T> TryLoadAsync(T defaultData)
        {
            if (File.Exists(FilePath))
            {
                if (new FileInfo(FilePath).Length == 0)
                {
                    Log.Error($"|BinaryStorage.TryLoad|Zero length cache file <{FilePath}>");
                    await SaveAsync(defaultData);
                    return defaultData;
                }

                await using var stream = new FileStream(FilePath, FileMode.Open);
                var d = await DeserializeAsync(stream, defaultData);
                return d;
            }
            else
            {
                Log.Info("|BinaryStorage.TryLoad|Cache file not exist, load default data");
                await SaveAsync(defaultData);
                return defaultData;
            }
        }

        private static async ValueTask<T> DeserializeAsync(Stream stream, T defaultData)
        {
            try
            {
                var t = await MemoryPackSerializer.DeserializeAsync<T>(stream);
                return t;
            }
            catch (System.Exception)
            {
                // Log.Exception($"|BinaryStorage.Deserialize|Deserialize error for file <{FilePath}>", e);
                return defaultData;
            }
        }

        public async ValueTask SaveAsync(T data)
        {
            await using var stream = new FileStream(FilePath, FileMode.Create);
            await MemoryPackSerializer.SerializeAsync(stream, data);
        }
    }
}
