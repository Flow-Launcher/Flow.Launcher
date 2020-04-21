using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Infrastructure.Storage
{
    public class Flow.LauncherJsonStorage<T> : JsonStrorage<T> where T : new()
    {
        public Flow.LauncherJsonStorage()
        {
            var directoryPath = Path.Combine(DataLocation.DataDirectory(), DirectoryName);
            Helper.ValidateDirectory(directoryPath);

            var filename = typeof(T).Name;
            FilePath = Path.Combine(directoryPath, $"{filename}{FileSuffix}");
        }
    }
}