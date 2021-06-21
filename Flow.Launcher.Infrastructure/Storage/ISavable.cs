using System;

namespace Flow.Launcher.Infrastructure.Storage
{
    [Obsolete("Deprecated as of Flow Launcher v1.8.0, on 2021.06.21. " +
        "This is used only for Everything plugin v1.4.9 or below backwards compatibility")]
    public interface ISavable : Plugin.ISavable { }
}