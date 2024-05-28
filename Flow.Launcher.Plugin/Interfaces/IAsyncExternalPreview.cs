using System.Threading.Tasks;

namespace Flow.Launcher.Plugin
{
    public interface IAsyncExternalPreview: IFeatures
    {
        public Task TogglePreviewAsync(string path);

        public Task OpenPreviewAsync(string path, bool sendFailToast = true);

        public Task ClosePreviewAsync();

        public Task SwitchPreviewAsync(string path, bool sendFailToast = true);
    }
}
