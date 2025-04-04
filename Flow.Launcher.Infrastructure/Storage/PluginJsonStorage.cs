using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Infrastructure.Storage
{
    public class PluginJsonStorage<T> : JsonStorage<T> where T : new()
    {
        // Use assembly name to check which plugin is using this storage
        public readonly string AssemblyName;

        private static readonly string ClassName = "PluginJsonStorage";

        // We should not initialize API in static constructor because it will create another API instance
        private static IPublicAPI api = null;
        private static IPublicAPI API => api ??= Ioc.Default.GetRequiredService<IPublicAPI>();

        public PluginJsonStorage()
        {
            // C# related, add python related below
            var dataType = typeof(T);
            AssemblyName = dataType.Assembly.GetName().Name;
            DirectoryPath = Path.Combine(DataLocation.PluginSettingsDirectory, AssemblyName);
            Helper.ValidateDirectory(DirectoryPath);

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
                API.LogException(ClassName, $"Failed to save plugin settings to path: {FilePath}", e);
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
                API.LogException(ClassName, $"Failed to save plugin settings to path: {FilePath}", e);
            }
        }
    }
}
