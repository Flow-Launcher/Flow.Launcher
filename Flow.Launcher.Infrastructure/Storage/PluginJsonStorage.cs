using System.IO;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Infrastructure.Storage
{
    // Expose ISaveable interface in derived class to make sure we are calling the new version of Save method
    public class PluginJsonStorage<T> : JsonStorage<T>, ISavable where T : new()
    {
        // Use assembly name to check which plugin is using this storage
        public readonly string AssemblyName;

        private static readonly string ClassName = "PluginJsonStorage";

        public PluginJsonStorage()
        {
            // C# related, add python related below
            var dataType = typeof(T);
            AssemblyName = dataType.Assembly.GetName().Name;
            DirectoryPath = Path.Combine(DataLocation.PluginSettingsDirectory, AssemblyName);
            FilesFolders.ValidateDirectory(DirectoryPath);

            FilePath = Path.Combine(DirectoryPath, $"{dataType.Name}{FileSuffix}");
        }

        public PluginJsonStorage(T data) : this()
        {
            Data = data;
        }

        public new void Save()
        {
            try
            {
                base.Save();
            }
            catch (System.Exception e)
            {
                Log.Exception(ClassName, $"Failed to save plugin settings to path: {FilePath}", e);
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
                Log.Exception(ClassName, $"Failed to save plugin settings to path: {FilePath}", e);
            }
        }
    }
}
