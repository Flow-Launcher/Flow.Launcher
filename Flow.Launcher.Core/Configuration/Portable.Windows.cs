using System;
using System.IO;
using System.Linq;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Microsoft.Win32;
using Squirrel;

namespace Flow.Launcher.Core.Configuration
{
    public partial class Portable
    {
        /// <summary>
        /// As at Squirrel.Windows version 1.5.2, UpdateManager needs to be disposed after finish
        /// </summary>
        /// <returns></returns>
        private static UpdateManager NewUpdateManager()
        {
            var applicationFolderName = Constant.ApplicationDirectory
                                            .Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.None)
                                            .Last();

            return new UpdateManager(string.Empty, applicationFolderName, Constant.RootDirectory);
        }

        public void DisablePortableMode()
        {
            try
            {
                MoveUserDataFolder(DataLocation.PortableDataPath, DataLocation.RoamingDataPath);
#if !DEBUG
                // Create shortcuts and uninstaller are not required in debug mode, 
                // otherwise will repoint the path of the actual installed production version to the debug version
                CreateShortcuts();
                CreateUninstallerEntry();
#endif
                IndicateDeletion(DataLocation.PortableDataPath);

                PublicApi.Instance.ShowMsgBox(Localize.restartToDisablePortableMode());

                UpdateManager.RestartApp(Constant.ApplicationFileName);
            }
            catch (Exception e)
            {
                PublicApi.Instance.LogException(ClassName, "Error occurred while disabling portable mode", e);
            }
        }

        public void EnablePortableMode()
        {
            try
            {
                MoveUserDataFolder(DataLocation.RoamingDataPath, DataLocation.PortableDataPath);
#if !DEBUG
                // Remove shortcuts and uninstaller are not required in debug mode, 
                // otherwise will delete the actual installed production version
                RemoveShortcuts();
                RemoveUninstallerEntry();
#endif
                IndicateDeletion(DataLocation.RoamingDataPath);

                PublicApi.Instance.ShowMsgBox(Localize.restartToEnablePortableMode());

                UpdateManager.RestartApp(Constant.ApplicationFileName);
            }
            catch (Exception e)
            {
                PublicApi.Instance.LogException(ClassName, "Error occurred while enabling portable mode", e);
            }
        }

        public void RemoveShortcuts()
        {
            using var portabilityUpdater = NewUpdateManager();
            portabilityUpdater.RemoveShortcutsForExecutable(Constant.ApplicationFileName, ShortcutLocation.StartMenu);
            portabilityUpdater.RemoveShortcutsForExecutable(Constant.ApplicationFileName, ShortcutLocation.Desktop);
            portabilityUpdater.RemoveShortcutsForExecutable(Constant.ApplicationFileName, ShortcutLocation.Startup);
        }

        public void RemoveUninstallerEntry()
        {
            using var portabilityUpdater = NewUpdateManager();
            portabilityUpdater.RemoveUninstallerRegistryEntry();
        }

        public void CreateShortcuts()
        {
            using var portabilityUpdater = NewUpdateManager();
            portabilityUpdater.CreateShortcutsForExecutable(Constant.ApplicationFileName, ShortcutLocation.StartMenu, false);
            portabilityUpdater.CreateShortcutsForExecutable(Constant.ApplicationFileName, ShortcutLocation.Desktop, false);
            portabilityUpdater.CreateShortcutsForExecutable(Constant.ApplicationFileName, ShortcutLocation.Startup, false);
        }

        public void CreateUninstallerEntry()
        {
            var uninstallRegSubKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
            using (var subKey1 = baseKey.CreateSubKey(uninstallRegSubKey, RegistryKeyPermissionCheck.ReadWriteSubTree))
            using (var subKey2 = subKey1.CreateSubKey(Constant.FlowLauncher, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                subKey2.SetValue("DisplayIcon", Path.Combine(Constant.ApplicationDirectory, "app.ico"), RegistryValueKind.String);
            }

            using var portabilityUpdater = NewUpdateManager();
            _ = portabilityUpdater.CreateUninstallerRegistryEntry();
        }
    }
}
