using System;
using System.IO;
using System.Windows;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Core.Plugin;
using Microsoft.Win32;
using Velopack.Windows;

namespace Flow.Launcher.Core.Configuration
{
    public class Portable : IPortable
    {
        public void DisablePortableMode()
        {
            try
            {
                MoveUserDataFolder(DataLocation.PortableDataPath, DataLocation.RoamingDataPath);
#if !DEBUG
                // Create shortcuts and uninstaller are not required in debug mode, 
                // otherwise will repoint the path of the actual installed production version to the debug version
                CreateShortcuts();
                // CreateUninstallerEntry();
#endif

                IndicateDeletion(DataLocation.PortableDataPath);

                MessageBox.Show("Flow Launcher needs to restart to finish disabling portable mode, " +
                                "after the restart your portable data profile will be deleted and roaming data profile kept");

                PluginManager.API.RestartApp();
            }
            catch (Exception e)
            {
                Log.Exception("|Portable.DisablePortableMode|Error occurred while disabling portable mode", e);
            }
        }

        public void CreateShortcuts()
        {
            var shortcuts = new Shortcuts();

            shortcuts.CreateShortcutForThisExe();
        }

        public void RemoveShortcuts()
        {
            var shortcuts = new Shortcuts();

            shortcuts.DeleteShortcuts(Velopack.Locators.VelopackLocator.GetDefault(null).ThisExeRelativePath,
                ShortcutLocation.Desktop | ShortcutLocation.StartMenu);
        }

        public void CreateUninstallerEntry()
        {
            var uninstallRegSubKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            using var subKey1 = baseKey.CreateSubKey(uninstallRegSubKey, RegistryKeyPermissionCheck.ReadWriteSubTree);
            using var subKey2 =
                subKey1.CreateSubKey(Constant.FlowLauncher, RegistryKeyPermissionCheck.ReadWriteSubTree);
            subKey2?.SetValue("DisplayIcon", Path.Combine(Constant.ApplicationDirectory, "app.ico"),
                RegistryValueKind.String);
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
                // RemoveUninstallerEntry();
#endif
                
                IndicateDeletion(DataLocation.RoamingDataPath);

                MessageBox.Show("Flow Launcher needs to restart to finish enabling portable mode, " +
                                "after the restart your roaming data profile will be deleted and portable data profile kept");

                PluginManager.API.RestartApp();
            }
            catch (Exception e)
            {
                Log.Exception("|Portable.EnablePortableMode|Error occurred while enabling portable mode", e);
            }
        }


        public void MoveUserDataFolder(string fromLocation, string toLocation)
        {
            FilesFolders.CopyAll(fromLocation, toLocation);
            VerifyUserDataAfterMove(fromLocation, toLocation);
        }

        public void VerifyUserDataAfterMove(string fromLocation, string toLocation)
        {
            FilesFolders.VerifyBothFolderFilesEqual(fromLocation, toLocation);
        }


        internal void IndicateDeletion(string filePathToDelete)
        {
            var deleteFilePath = Path.Combine(filePathToDelete, DataLocation.DeletionIndicatorFile);
            using (var _ = File.CreateText(deleteFilePath))
            {
            }
        }

        ///<summary>
        ///This method should be run at first before all methods during start up and should be run before determining which data location
        ///will be used for Flow Launcher.
        ///</summary>
        public void PreStartCleanUpAfterPortabilityUpdate()
        {
            // Specify here so this method does not rely on other environment variables to initialise
            var portableDataDir = DataLocation.PortableDataPath;
            var roamingDataDir = DataLocation.RoamingDataPath;


            // Get full path to the .dead files for each case
            var portableDataDeleteFilePath = Path.Combine(portableDataDir, DataLocation.DeletionIndicatorFile);
            var roamingDataDeleteFilePath = Path.Combine(roamingDataDir, DataLocation.DeletionIndicatorFile);

            // If the data folder in %AppData% is marked for deletion,
            // delete it and prompt the user to pick the portable data location
            if (File.Exists(roamingDataDeleteFilePath))
            {
                FilesFolders.RemoveFolderIfExists(roamingDataDir);

                if (MessageBox.Show("Flow Launcher has detected you enabled portable mode, " +
                                    "would you like to move it to a different location?", string.Empty,
                        MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    FilesFolders.OpenPath(Constant.RootDirectory);

                    Environment.Exit(0);
                }
            }
            // Otherwise, if the portable data folder is marked for deletion,
            // delete it and notify the user about it.
            else if (File.Exists(portableDataDeleteFilePath))
            {
                FilesFolders.RemoveFolderIfExists(portableDataDir);

                MessageBox.Show("Flow Launcher has detected you disabled portable mode, " +
                                "the relevant shortcuts and uninstaller entry have been created");
            }
        }

        public bool CanUpdatePortability()
        {
            var roamingLocationExists = DataLocation.RoamingDataPath.LocationExists();
            var portableLocationExists = DataLocation.PortableDataPath.LocationExists();

            if (roamingLocationExists && portableLocationExists)
            {
                MessageBox.Show(string.Format("Flow Launcher detected your user data exists both in {0} and " +
                                              "{1}. {2}{2}Please delete {1} in order to proceed. No changes have occurred.",
                    DataLocation.PortableDataPath, DataLocation.RoamingDataPath, Environment.NewLine));

                return false;
            }

            return true;
        }
    }
}
