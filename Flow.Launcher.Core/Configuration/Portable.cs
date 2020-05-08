using Microsoft.Win32;
using Squirrel;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedCommands;
using System.Linq;

namespace Flow.Launcher.Core.Configuration
{
    public class Portable : IPortable
    {
        /// <summary>
        /// As at Squirrel.Windows version 1.5.2, UpdateManager needs to be disposed after finish
        /// </summary>
        /// <returns></returns>
        private UpdateManager NewUpdateManager()
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
#if DEBUG
                // Create shortcuts and uninstaller are not required in debug mode, 
                // otherwise will repoint the path of the actual installed production version to the debug version
#else
                CreateShortcuts();
                CreateUninstallerEntry();
#endif
                IndicateDeletion(DataLocation.PortableDataPath);

                MessageBox.Show("Flow Launcher needs to restart to finish disabling portable mode, " +
                    "after the restart your portable data profile will be deleted and roaming data profile kept");

                UpdateManager.RestartApp(Constant.ApplicationFileName);
            }
            catch (Exception e)
            {
#if !DEBUG
                Log.Exception("Portable", "Error occured while disabling portable mode", e);
#endif
                throw;
            }
        }

        public void EnablePortableMode()
        {
            try
            {
                MoveUserDataFolder(DataLocation.RoamingDataPath, DataLocation.PortableDataPath);
#if DEBUG
                // Remove shortcuts and uninstaller are not required in debug mode, 
                // otherwise will delete the actual installed production version
#else
                RemoveShortcuts();
                RemoveUninstallerEntry();
#endif
                IndicateDeletion(DataLocation.RoamingDataPath);

                MessageBox.Show("Flow Launcher needs to restart to finish enabling portable mode, " +
                    "after the restart your roaming data profile will be deleted and portable data profile kept");

                UpdateManager.RestartApp(Constant.ApplicationFileName);
            }
            catch (Exception e)
            {
#if !DEBUG
                Log.Exception("Portable", "Error occured while enabling portable mode", e);
#endif
                throw;
            }
        }

        public void RemoveShortcuts()
        {
            using (var portabilityUpdater = NewUpdateManager())
            {
                portabilityUpdater.RemoveShortcutsForExecutable(Constant.ApplicationFileName, ShortcutLocation.StartMenu);
                portabilityUpdater.RemoveShortcutsForExecutable(Constant.ApplicationFileName, ShortcutLocation.Desktop);
                portabilityUpdater.RemoveShortcutsForExecutable(Constant.ApplicationFileName, ShortcutLocation.Startup);
            }
        }

        public void RemoveUninstallerEntry()
        {
            using (var portabilityUpdater = NewUpdateManager())
            {
                portabilityUpdater.RemoveUninstallerRegistryEntry();
            }
        }

        public void MoveUserDataFolder(string fromLocation, string toLocation)
        {
            FilesFolders.Copy(fromLocation, toLocation);
            VerifyUserDataAfterMove(fromLocation, toLocation);
        }

        public void VerifyUserDataAfterMove(string fromLocation, string toLocation)
        {
            FilesFolders.VerifyBothFolderFilesEqual(fromLocation, toLocation);
        }

        public void CreateShortcuts()
        {
            using (var portabilityUpdater = NewUpdateManager())
            {
                portabilityUpdater.CreateShortcutsForExecutable(Constant.ApplicationFileName, ShortcutLocation.StartMenu, false);
                portabilityUpdater.CreateShortcutsForExecutable(Constant.ApplicationFileName, ShortcutLocation.Desktop, false);
                portabilityUpdater.CreateShortcutsForExecutable(Constant.ApplicationFileName, ShortcutLocation.Startup, false);
            }
        }

        public void CreateUninstallerEntry()
        {
            var uninstallRegSubKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
            // NB: Sometimes the Uninstall key doesn't exist
            using (var parentKey =
                RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default)
                    .CreateSubKey("Uninstall", RegistryKeyPermissionCheck.ReadWriteSubTree)) {; }

            var key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default)
                .CreateSubKey(uninstallRegSubKey + "\\" + Constant.FlowLauncher, RegistryKeyPermissionCheck.ReadWriteSubTree);
            key.SetValue("DisplayIcon", Constant.ApplicationDirectory + "\\app.ico", RegistryValueKind.String);

            using (var portabilityUpdater = NewUpdateManager())
            {
                portabilityUpdater.CreateUninstallerRegistryEntry();
            }
        }

        internal void IndicateDeletion(string filePathTodelete)
        {
            using (StreamWriter sw = File.CreateText(filePathTodelete + "\\" + DataLocation.DeletionIndicatorFile)){}
        }

        ///<summary>
        ///This method should be run at first before all methods during start up and should be run before determining which data location
        ///will be used for Flow Launcher.
        ///</summary>
        public void PreStartCleanUpAfterPortabilityUpdate()
        {
            // Specify here so this method does not rely on other environment variables to initialise
            var portableDataPath = Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location.NonNull()).ToString(), "UserData");
            var roamingDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlowLauncher");

            bool DataLocationPortableDeleteRequired = false;
            bool DataLocationRoamingDeleteRequired = false;

            if ((roamingDataPath + "\\" + DataLocation.DeletionIndicatorFile).FileExits())
                DataLocationRoamingDeleteRequired = true;

            if ((portableDataPath + "\\" + DataLocation.DeletionIndicatorFile).FileExits())
                DataLocationPortableDeleteRequired = true;

            if (DataLocationRoamingDeleteRequired)
            {
                FilesFolders.RemoveFolderIfExists(roamingDataPath);

                if (MessageBox.Show("Flow Launcher has detected you enabled portable mode, " +
                                    "would you like to move it to a different location?", string.Empty,
                                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    FilesFolders.OpenPath(Constant.RootDirectory);

                    Environment.Exit(0);
                }

                return;
            }

            if(DataLocationPortableDeleteRequired)
            {
                FilesFolders.RemoveFolderIfExists(portableDataPath);

                MessageBox.Show("Flow Launcher has detected you disabled portable mode, " +
                                    "the relevant shortcuts and uninstaller entry have been created");

                return;
            }
        }

        public bool CanUpdatePortability()
        {
            var roamingLocationExists = DataLocation.RoamingDataPath.LocationExists();
            var portableLocationExists = DataLocation.PortableDataPath.LocationExists();

            if(roamingLocationExists && portableLocationExists)
            {
                MessageBox.Show(string.Format("Flow Launcher detected your user data exists both in {0} and " +
                                    "{1}. {2}{2}Please delete {1} in order to proceed. No changes have occured.", 
                                    DataLocation.PortableDataPath, DataLocation.RoamingDataPath, Environment.NewLine));

                return false;
            }

            return true;
        }
    }
}
