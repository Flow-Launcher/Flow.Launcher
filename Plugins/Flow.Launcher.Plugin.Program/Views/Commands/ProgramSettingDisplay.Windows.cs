using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Program.Views.Models;

namespace Flow.Launcher.Plugin.Program.Views.Commands
{
    internal static partial class ProgramSettingDisplay
    {
        private static async partial Task DisplayAllUwpProgramsAsync()
        {
            await Main._uwpsLock.WaitAsync();
            try
            {
                var uwp = Main._uwps
                            .Where(t1 => !ProgramSetting.ProgramSettingDisplayList.Any(x => x.UniqueIdentifier == t1.UniqueIdentifier))
                            .Select(x => new ProgramSource(x));
                ProgramSetting.ProgramSettingDisplayList.AddRange(uwp);
            }
            finally
            {
                Main._uwpsLock.Release();
            }
        }

        private static async partial Task SetUwpProgramsStatusAsync(List<ProgramSource> selectedProgramSourcesToDisable, bool status)
        {
            await Main._uwpsLock.WaitAsync();
            try
            {
                foreach (var program in Main._uwps)
                {
                    if (selectedProgramSourcesToDisable.Any(x => x.UniqueIdentifier == program.UniqueIdentifier && program.Enabled != status))
                    {
                        program.Enabled = status;
                    }
                }
            }
            finally
            {
                Main._uwpsLock.Release();
            }
        }

        private static async partial Task<bool> IsNotInCacheAsync(List<ProgramSource> selectedItems)
        {
            // Not in cache
            await Main._win32sLock.WaitAsync();
            await Main._uwpsLock.WaitAsync();
            try
            {
                if (selectedItems.Any(t1 => t1.Enabled && !Main._uwps.Any(x => t1.UniqueIdentifier == x.UniqueIdentifier))
                && selectedItems.Any(t1 => t1.Enabled && !Main._win32s.Any(x => t1.UniqueIdentifier == x.UniqueIdentifier)))
                    return true;
            }
            finally
            {
                Main._win32sLock.Release();
                Main._uwpsLock.Release();
            }

            return false;
        }
    }
}
