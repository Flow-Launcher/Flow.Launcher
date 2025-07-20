using System.IO;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Infrastructure.Storage
{
    // Expose ISaveable interface in derived class to make sure we are calling the new version of Save method
    public class FlowLauncherJsonStorage<T> : JsonStorage<T>, ISavable where T : new()
    {
        private static readonly string ClassName = "FlowLauncherJsonStorage";

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
                Log.Exception(ClassName, $"Failed to save FL settings to path: {FilePath}", e);
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
                Log.Exception(ClassName, $"Failed to save FL settings to path: {FilePath}", e);
            }
        }
    }
}
