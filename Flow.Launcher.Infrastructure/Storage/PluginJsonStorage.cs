using System.IO;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Infrastructure.Storage
{
    public class PluginJsonStorage<T> : JsonStorage<T> where T : new()
    {
        // Use assembly name to check which plugin is using this storage
        public readonly string AssemblyName;

        public PluginJsonStorage()
        {
            var dataType = typeof(T);
            AssemblyName = dataType.Assembly.GetName().Name;
            DirectoryPath = Path.Combine(DataLocation.PluginSettingsDirectory, AssemblyName);
            Helper.ValidateDirectory(DirectoryPath);

            FilePath = Path.Combine(DirectoryPath, $"{dataType.Name}{FileSuffix}");
        }
    }
}
