using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Program.Views.Models;

namespace Flow.Launcher.Plugin.Program.Views.Commands
{
    // There are no UWP programs outside Windows
    internal static partial class ProgramSettingDisplay
    {
        private static partial Task DisplayAllUwpProgramsAsync()
        {
            return Task.CompletedTask;
        }

        private static partial Task SetUwpProgramsStatusAsync(List<ProgramSource> selectedProgramSourcesToDisable, bool status)
        {
            return Task.CompletedTask;
        }

        private static async partial Task<bool> IsNotInCacheAsync(List<ProgramSource> selectedItems)
        {
            // Not in cache
            await Main._win32sLock.WaitAsync();
            try
            {
                return selectedItems.Any(t1 => t1.Enabled && !Main._win32s.Any(x => t1.UniqueIdentifier == x.UniqueIdentifier));
            }
            finally
            {
                Main._win32sLock.Release();
            }
        }
    }
}
