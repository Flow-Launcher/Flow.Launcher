using System.Globalization;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Represent plugins that support internationalization
    /// </summary>
    public interface IPluginI18n : IFeatures
    {
        string GetTranslatedPluginTitle();

        string GetTranslatedPluginDescription();

        /// <summary>
        /// The method will be invoked when language of flow changed
        /// </summary>
        void OnCultureInfoChanged(CultureInfo newCulture)
        {

        }
    }
}