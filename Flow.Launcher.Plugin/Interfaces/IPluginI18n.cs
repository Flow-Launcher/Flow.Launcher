using System.Globalization;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Represent plugins that support internationalization
    /// </summary>
    public interface IPluginI18n : IFeatures
    {
        /// <summary>
        /// Get a localised version of the plugin's title
        /// </summary>
        string GetTranslatedPluginTitle();

        /// <summary>
        /// Get a localised version of the plugin's description
        /// </summary>
        string GetTranslatedPluginDescription();

        /// <summary>
        /// The method will be invoked when language of flow changed
        /// </summary>
        void OnCultureInfoChanged(CultureInfo newCulture)
        {

        }
    }
}