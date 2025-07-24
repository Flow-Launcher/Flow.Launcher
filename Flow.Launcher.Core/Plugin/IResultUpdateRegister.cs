using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin;

public interface IResultUpdateRegister
{
    /// <summary>
    /// Register a plugin to receive results updated event.
    /// </summary>
    /// <param name="pair"></param>
    void RegisterResultsUpdatedEvent(PluginPair pair);
}
