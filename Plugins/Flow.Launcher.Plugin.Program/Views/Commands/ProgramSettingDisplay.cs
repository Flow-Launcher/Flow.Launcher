using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            Main._win32s
                .Where(t1 => !ProgramSetting.ProgramSettingDisplayList.Any(x => x.UniqueIdentifier == t1.UniqueIdentifier))
                .ToList()
                .ForEach(t1 => ProgramSetting.ProgramSettingDisplayList.Add(new ProgramSource(t1)));

            Main._uwps
                .Where(t1 => !ProgramSetting.ProgramSettingDisplayList.Any(x => x.UniqueIdentifier == t1.UniqueIdentifier))
                .ToList()
                .ForEach(t1 => ProgramSetting.ProgramSettingDisplayList.Add(new ProgramSource(t1)));
        }

        internal static void SetProgramSourcesStatus(List<ProgramSource> selectedProgramSourcesToDisable, bool status)
        {
            ProgramSetting.ProgramSettingDisplayList
                .Where(t1 => selectedProgramSourcesToDisable.Any(x => x.UniqueIdentifier == t1.UniqueIdentifier && t1.Enabled != status))
                .ToList()
                .ForEach(t1 => t1.Enabled = status);

            Main._win32s
                .Where(t1 => selectedProgramSourcesToDisable.Any(x => x.UniqueIdentifier == t1.UniqueIdentifier && t1.Enabled != status))
                .ToList()
                .ForEach(t1 => t1.Enabled = status);

            Main._uwps
                .Where(t1 => selectedProgramSourcesToDisable.Any(x => x.UniqueIdentifier == t1.UniqueIdentifier && t1.Enabled != status))
                .ToList()
                .ForEach(t1 => t1.Enabled = status);
        }

        internal static void StoreDisabledInSettings()
        {
            // no need since using refernce now
            //Main._settings.ProgramSources
            //   .Where(t1 => ProgramSetting.ProgramSettingDisplayList.Any(x => x.UniqueIdentifier == t1.UniqueIdentifier && !x.Enabled))
            //   .ToList()
            //   .ForEach(t1 => t1.Enabled = false);

            // Disabled, not in DisabledProgramSources or ProgramSources
            var tmp = ProgramSetting.ProgramSettingDisplayList
                .Where(t1 => !t1.Enabled
                                && !Main._settings.DisabledProgramSources.Any(x => x.UniqueIdentifier == t1.UniqueIdentifier)
                                && !Main._settings.ProgramSources.Any(x => x.UniqueIdentifier == t1.UniqueIdentifier));

            Main._settings.DisabledProgramSources.AddRange(tmp);
        }

        internal static void RemoveDisabledFromSettings()
        {
            //Main._settings.ProgramSources
            //   .Where(t1 => ProgramSetting.ProgramSettingDisplayList.Any(x => x.UniqueIdentifier == t1.UniqueIdentifier && x.Enabled))
            //   .ToList()
            //   .ForEach(t1 => t1.Enabled = true);

            Main._settings.DisabledProgramSources
                .RemoveAll(t1 => t1.Enabled);
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
