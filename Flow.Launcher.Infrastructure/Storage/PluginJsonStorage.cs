using System.IO;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Infrastructure.Storage
{
    public class PluginJsonStorage<T> :JsonStorage<T> where T : new()
    {
        public PluginJsonStorage()
        {
            // C# related, add python related below
            var dataType = typeof(T);
            var assemblyName = dataType.Assembly.GetName().Name;
            DirectoryPath = Path.Combine(DataLocation.DataDirectory(), DirectoryName, Constant.Plugins, assemblyName);
            Helper.ValidateDirectory(DirectoryPath);

            FilePath = Path.Combine(DirectoryPath, $"{dataType.Name}{FileSuffix}");
        }

        public PluginJsonStorage(T data) : this()
        {
            Data = data;
        }
    }
}

