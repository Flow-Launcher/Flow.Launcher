using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Infrastructure.Storage
{
    public class FlowLauncherJsonStorage<T> : JsonStorage<T> where T : new()
    {
        private static readonly string ClassName = "FlowLauncherJsonStorage";

        // We should not initialize API in static constructor because it will create another API instance
        private static IPublicAPI api = null;
        private static IPublicAPI API => api ??= Ioc.Default.GetRequiredService<IPublicAPI>();

        public FlowLauncherJsonStorage()
        {
            DirectoryPath = Path.Combine(DataLocation.DataDirectory(), DirectoryName);
            FilesFolders.ValidateDirectory(DirectoryPath);

            var filename = typeof(T).Name;
            FilePath = Path.Combine(DirectoryPath, $"{filename}{FileSuffix}");
        }

        public new void Save()
        {
            try
            {
                base.Save();
            }
            catch (System.Exception e)
            {
                API.LogException(ClassName, $"Failed to save FL settings to path: {FilePath}", e);
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
                API.LogException(ClassName, $"Failed to save FL settings to path: {FilePath}", e);
            }
        }
    }
}
