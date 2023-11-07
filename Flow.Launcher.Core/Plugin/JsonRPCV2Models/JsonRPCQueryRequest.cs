using System.Collections.Generic;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin.JsonRPCV2Models
{
    public record JsonRPCQueryRequest(
        List<JsonRPCResult> Results
    );
}
