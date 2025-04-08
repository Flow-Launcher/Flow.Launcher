using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Infrastructure.Storage
{
    public class PluginBinaryStorage<T> : BinaryStorage<T> where T : new()
    {
        private static readonly string ClassName = "PluginBinaryStorage";

        // We should not initialize API in static constructor because it will create another API instance
        private static IPublicAPI api = null;
        private static IPublicAPI API => api ??= Ioc.Default.GetRequiredService<IPublicAPI>();

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
                API.LogException(ClassName, $"Failed to save plugin caches to path: {FilePath}", e);
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
                API.LogException(ClassName, $"Failed to save plugin caches to path: {FilePath}", e);
            }
        }
    }
}
