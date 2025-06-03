using System.IO;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Infrastructure.Storage
{
    public class PluginBinaryStorage<T> : BinaryStorage<T> where T : new()
    {
        private static readonly string ClassName = "PluginBinaryStorage";

        public PluginBinaryStorage(string cacheName, string cacheDirectory)
        {
            DirectoryPath = cacheDirectory;
            FilesFolders.ValidateDirectory(DirectoryPath);

            FilePath = Path.Combine(DirectoryPath, $"{cacheName}{FileSuffix}");
        }

        public new void Save()
        {
            try
            {
                base.Save();
            }
            catch (System.Exception e)
            {
                Log.Exception(ClassName, $"Failed to save plugin caches to path: {FilePath}", e);
            }
        }

        public new async Task SaveAsync()
        {
            try
            {
                await base.SaveAsync();
            }
            catch (System.Exception e)
            {
                Log.Exception(ClassName, $"Failed to save plugin caches to path: {FilePath}", e);
            }
        }
    }
}
