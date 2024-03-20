using Flow.Launcher.Plugin;
using System.Collections.Generic;
using System.Threading;

namespace Flow.Launcher.ViewModel
{
    public record struct ResultsForUpdate(
        IReadOnlyList<Result> Results,
        PluginMetadata Metadata,
        Query Query,
        CancellationToken Token,
        bool ReSelectFirstResult = true)
    {
        public string ID { get; } = Metadata.ID;
    }
}
