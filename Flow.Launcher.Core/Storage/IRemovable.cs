namespace Flow.Launcher.Core.Storage;

/// <summary>
/// Remove storage instances from <see cref="Launcher.Plugin.IPublicAPI"/> instance
/// </summary>
public interface IRemovable
{
    /// <summary>
    /// Remove all <see cref="Infrastructure.Storage.PluginJsonStorage{T}"/> instances of one plugin
    /// </summary>
    /// <param name="assemblyName"></param>
    public void RemovePluginSettings(string assemblyName);

    /// <summary>
    /// Remove all <see cref="Infrastructure.Storage.PluginBinaryStorage{T}"/> instances of one plugin
    /// </summary>
    /// <param name="cacheDirectory"></param>
    public void RemovePluginCaches(string cacheDirectory);
}
