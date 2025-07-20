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

        /// <summary>
        /// Allows the preview plugin to override the AlwaysPreview setting. Typically useful if plugin's preview does not
        /// fully work well with being shown together when the query window appears with results.
        /// When AlwaysPreview setting is on and this is set to false, the preview will not be shown when query 
        /// window appears with results, instead the internal preview will be shown.
        /// </summary>
        /// <returns></returns>
        public bool AllowAlwaysPreview();
    }
}
