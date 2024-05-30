using System.Threading.Tasks;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// This interface is for plugins that wish to provide file preview (external preview) 
    /// via a third party app instead of the default preview.
    /// </summary>
    public interface IAsyncExternalPreview : IFeatures
    {
        /// <summary>
        /// Method for opening/showing the preview.
        /// </summary>
        /// <param name="path">The file path to open the preview for</param>
        /// <param name="sendFailToast">Whether to send a toast message notification on failure for the user</param>
        public Task OpenPreviewAsync(string path, bool sendFailToast = true);

        /// <summary>
        /// Method for closing/hiding the preview.
        /// </summary>
        public Task ClosePreviewAsync();

        /// <summary>
        /// Method for switching the preview to the next file result. 
        /// This requires the external preview be already open/showing
        /// </summary>
        /// <param name="path">The file path to switch the preview for</param>
        /// <param name="sendFailToast">Whether to send a toast message notification on failure for the user</param>
        public Task SwitchPreviewAsync(string path, bool sendFailToast = true);
    }
}
