using System.IO;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Infrastructure.Storage
{
    public class FlowLauncherJsonStorage<T> : JsonStorage<T> where T : new()
    {
        public FlowLauncherJsonStorage()
        {
            var directoryPath = Path.Combine(DataLocation.DataDirectory(), DirectoryName);
            Helper.ValidateDirectory(directoryPath);

            var filename = typeof(T).Name;
            FilePath = Path.Combine(directoryPath, $"{filename}{FileSuffix}");
        }
    }
}