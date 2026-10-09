using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Program.Programs;

namespace Flow.Launcher.Plugin.Program
{
    // UWP packages only exist on Windows; outside Windows the Win32 list holds the indexed applications.
    public partial class Main
    {
        private partial Task<ParallelQuery<IProgram>> GetProgramsToQueryAsync(List<Win32> win32s, CancellationToken token)
        {
            return Task.FromResult(win32s.Cast<IProgram>()
                .AsParallel()
                .WithCancellation(token));
        }

        private static partial Task<bool> LoadUwpCacheAsync(string pluginCacheDirectory)
        {
            // No UWP cache to wait for
            return Task.FromResult(false);
        }

        private static partial Task IndexUwpProgramsInBackgroundAsync()
        {
            return Task.CompletedTask;
        }

        private static partial Task<bool> DisableUwpProgramAsync(IProgram programToDelete)
        {
            return Task.FromResult(false);
        }
    }
}
