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

        void OnCultureInfoChanged(CultureInfo newCulture)
        {

        }
    }
}