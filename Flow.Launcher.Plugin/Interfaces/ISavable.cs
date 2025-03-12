namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Inherit this interface if additional data e.g. cache needs to be saved.
    /// </summary>
    /// <remarks>
    /// For storing plugin settings, prefer <see cref="IPublicAPI.LoadSettingJsonStorage{T}"/>
    /// or <see cref="IPublicAPI.SaveSettingJsonStorage{T}"/>. 
    /// Once called, your settings will be automatically saved by Flow.
    /// </remarks>
    public interface ISavable : IFeatures
    {
        /// <summary>
        /// Save additional plugin data, such as cache.
        /// </summary>
        void Save();
    }
}