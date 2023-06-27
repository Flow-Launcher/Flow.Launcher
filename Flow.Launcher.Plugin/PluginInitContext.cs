namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Carries data passed to a plugin when it gets initialized.
    /// </summary>
    public class PluginInitContext
    {
        public PluginInitContext()
        {
        }

        public PluginInitContext(PluginMetadata currentPluginMetadata, IPublicAPI api)
        {
            CurrentPluginMetadata = currentPluginMetadata;
            API = api;
        }

        /// <summary>
        /// The metadata of the plugin being initialized.
        /// </summary>
        public PluginMetadata CurrentPluginMetadata { get; internal set; }

        /// <summary>
        /// Public APIs for plugin invocation
        /// </summary>
        public IPublicAPI API { get; set; }
    }
}
