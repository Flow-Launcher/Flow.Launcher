namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Carries data passed to a plugin when it gets initialized.
    /// </summary>
    public class PluginInitContext
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public PluginInitContext()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="currentPluginMetadata"></param>
        /// <param name="api"></param>
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
