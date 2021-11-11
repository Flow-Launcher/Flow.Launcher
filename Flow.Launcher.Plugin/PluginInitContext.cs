namespace Flow.Launcher.Plugin
{
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

        public PluginMetadata CurrentPluginMetadata { get; internal set; }

        /// <summary>
        /// Public APIs for plugin invocation
        /// </summary>
        public IPublicAPI API { get; set; }
    }
}
