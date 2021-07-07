namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Save addtional plugin data. Inherit this interface if additional data e.g. cache needs to be saved,
    /// Otherwise if LoadSettingJsonStorage or SaveSettingJsonStorage has been callded,
    /// plugin settings will be automatically saved (see Flow.Launcher/PublicAPIInstance.SavePluginSettings) by Flow
    /// </summary>
    public interface ISavable : IFeatures
    {
        void Save();
    }
}