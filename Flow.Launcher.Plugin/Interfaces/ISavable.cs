namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Inherit this interface if you need to save additional data which is not a setting or cache,
    /// please implement this interface.
    /// </summary>
    /// <remarks>
    /// For storing plugin settings, prefer <see cref="IPublicAPI.LoadSettingJsonStorage{T}"/>
    /// or <see cref="IPublicAPI.SaveSettingJsonStorage{T}"/>.
    /// For storing plugin caches, prefer <see cref="IPublicAPI.LoadCacheBinaryStorageAsync{T}"/>
    /// or <see cref="IPublicAPI.SaveCacheBinaryStorageAsync{T}(string, string)"/>.
    /// Once called, those settings and caches will be automatically saved by Flow.
    /// </remarks>
    public interface ISavable : IFeatures
    {
        /// <summary>
        /// Save additional plugin data.
        /// </summary>
        void Save();
    }
}
