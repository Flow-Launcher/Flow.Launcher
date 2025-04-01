using System.IO;

namespace Flow.Launcher.Infrastructure.Storage
{
    public class PluginBinaryStorage<T> : BinaryStorage<T> where T : new()
    {
        public PluginBinaryStorage(string cacheName, string cacheDirectory)
        {
            DirectoryPath = cacheDirectory;
            Helper.ValidateDirectory(DirectoryPath);

            FilePath = Path.Combine(DirectoryPath, $"{cacheName}{FileSuffix}");
        }
    }
}
