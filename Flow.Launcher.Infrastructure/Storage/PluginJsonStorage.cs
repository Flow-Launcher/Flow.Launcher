using System.IO;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Infrastructure.Storage
{
    public class PluginJsonSettingStorage<T> :JsonStorage<T> where T : new()
    {
        public PluginJsonSettingStorage()
        {
            // C# related, add python related below
            var dataType = typeof(T);
            var assemblyName = dataType.Assembly.GetName().Name;
            DirectoryPath = Path.Combine(DataLocation.DataDirectory(), DirectoryName, Constant.Plugins, assemblyName);
            Helper.ValidateDirectory(DirectoryPath);

            FilePath = Path.Combine(DirectoryPath, $"{dataType.Name}{FileSuffix}");
        }

        public PluginJsonSettingStorage(T data) : this()
        {
            _data = data;
        }
    }

    public class PluginJsonStorage<T> : JsonStrorage<T> where T : new()
    {
        public PluginJsonStorage()
        {
            // C# releated, add python releated below
            var dataType = typeof(T);
            var assemblyName = typeof(T).Assembly.GetName().Name;
            DirectoryPath = Path.Combine(DataLocation.DataDirectory(), DirectoryName, Constant.Plugins, assemblyName);
            Helper.ValidateDirectory(DirectoryPath);

            FilePath = Path.Combine(DirectoryPath, $"{dataType.Name}{FileSuffix}");
        }
    }

}

