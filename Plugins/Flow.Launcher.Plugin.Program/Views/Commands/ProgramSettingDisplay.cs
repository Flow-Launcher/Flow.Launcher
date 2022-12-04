using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Plugin.Program.Views.Models;

namespace Flow.Launcher.Plugin.Program.Views.Commands
{
    internal static class ProgramSettingDisplay
    {
        internal static List<ProgramSource> LoadProgramSources()
        {
            // Even though these are disabled, we still want to display them so users can enable later on
            return Main._settings
                       .DisabledProgramSources
                       .Union(Main._settings.ProgramSources)
                       .ToList();
        }

        internal static void DisplayAllPrograms()
        {
            var win32 = Main._win32s
                            .Where(t1 => !ProgramSetting.ProgramSettingDisplayList.Any(x => x.UniqueIdentifier == t1.UniqueIdentifier))
                            .Select(x => new ProgramSource(x));

            var uwp = Main._uwps
                            .Where(t1 => !ProgramSetting.ProgramSettingDisplayList.Any(x => x.UniqueIdentifier == t1.UniqueIdentifier))
                            .Select(x => new ProgramSource(x));

            ProgramSetting.ProgramSettingDisplayList.AddRange(win32);
            ProgramSetting.ProgramSettingDisplayList.AddRange(uwp);
        }

        internal static void SetProgramSourcesStatus(List<ProgramSource> selectedProgramSourcesToDisable, bool status)
        {
            foreach(var program in ProgramSetting.ProgramSettingDisplayList)
            {
                if (selectedProgramSourcesToDisable.Any(x => x.UniqueIdentifier == program.UniqueIdentifier && program.Enabled != status))
                {
                    program.Enabled = status;
                }
            }

            foreach(var program in Main._win32s)
            {
                if (selectedProgramSourcesToDisable.Any(x => x.UniqueIdentifier == program.UniqueIdentifier && program.Enabled != status))
                {
                    program.Enabled = status;
                }
            }

            foreach (var program in Main._uwps)
            {
                if (selectedProgramSourcesToDisable.Any(x => x.UniqueIdentifier == program.UniqueIdentifier && program.Enabled != status))
                {
                    program.Enabled = status;
                }
            }
        }

        internal static void StoreDisabledInSettings()
        {
            // Disabled, not in DisabledProgramSources or ProgramSources
            var tmp = ProgramSetting.ProgramSettingDisplayList
                .Where(t1 => !t1.Enabled
                                && !Main._settings.DisabledProgramSources.Any(x => x.UniqueIdentifier == t1.UniqueIdentifier)
                                && !Main._settings.ProgramSources.Any(x => x.UniqueIdentifier == t1.UniqueIdentifier));

            Main._settings.DisabledProgramSources.AddRange(tmp);
        }

        internal static void RemoveDisabledFromSettings()
        {
            Main._settings.DisabledProgramSources.RemoveAll(t1 => t1.Enabled);
        }

        internal static bool IsReindexRequired(this List<ProgramSource> selectedItems)
        {
            // Not in cache
            if (selectedItems.Any(t1 => t1.Enabled && !Main._uwps.Any(x => t1.UniqueIdentifier == x.UniqueIdentifier))
                && selectedItems.Any(t1 => t1.Enabled && !Main._win32s.Any(x => t1.UniqueIdentifier == x.UniqueIdentifier)))
                return true;

            // ProgramSources holds list of user added directories, 
            // so when we enable/disable we need to reindex to show/not show the programs
            // that are found in those directories.
            if (selectedItems.Any(t1 => Main._settings.ProgramSources.Any(x => t1.UniqueIdentifier == x.UniqueIdentifier)))
                return true;

            return false;
        }
    }
}
