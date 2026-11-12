using System.Threading.Tasks;

namespace Flow.Launcher.Core
{
    public partial class Updater
    {
        // Updates are delivered through Squirrel.Windows; there is no updater for this platform yet.
        public partial Task UpdateAppAsync(bool silentUpdate)
        {
            _api.LogWarn(ClassName, "Application update is not supported on this platform");

            if (!silentUpdate)
                _api.ShowMsgError(Localize.update_flowlauncher_fail());

            return Task.CompletedTask;
        }
    }
}
