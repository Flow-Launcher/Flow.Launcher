using System.Threading.Tasks;

namespace Flow.Launcher.Plugin
{
    public interface IAsyncReloadable
    {
        Task ReloadDataAsync();
    }
}