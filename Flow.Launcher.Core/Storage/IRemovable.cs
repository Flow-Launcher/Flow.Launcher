using System;

namespace Flow.Launcher.Core.Storage;

/// <summary>
/// Remove per-plugin state (storages, callbacks) from <see cref="Launcher.Plugin.IPublicAPI"/> instance
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

    /// <summary>
    /// Remove every callback and event handler a plugin registered through the API and did not remove itself,
    /// such as global keyboard callbacks and <see cref="Launcher.Plugin.IPublicAPI.VisibilityChanged"/> handlers
    /// </summary>
    /// <param name="belongsToPlugin">Whether a handler's code comes from the plugin being unloaded</param>
    public void RemovePluginCallbacks(Func<Delegate, bool> belongsToPlugin);
}
