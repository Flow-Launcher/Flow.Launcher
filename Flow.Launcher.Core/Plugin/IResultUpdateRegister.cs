using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin;

public interface IResultUpdateRegister
{
    /// <summary>
    /// Register a plugin to receive results updated event.
    /// </summary>
    /// <param name="pair"></param>
    void RegisterResultsUpdatedEvent(PluginPair pair);

    /// <summary>
    /// Unregister a plugin from the results updated event, e.g. before the plugin is unloaded or reloaded.
    /// </summary>
    /// <param name="pair"></param>
    void UnregisterResultsUpdatedEvent(PluginPair pair);
}
