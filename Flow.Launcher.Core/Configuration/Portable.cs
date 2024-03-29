using Microsoft.Win32;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedCommands;
using System.Linq;
using Flow.Launcher.Core.Plugin;
using Velopack;
using Velopack.Locators;
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

        public void EnablePortableMode()
        {
            try
            {
                MoveUserDataFolder(DataLocation.RoamingDataPath, DataLocation.PortableDataPath);
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
            // check whether the package locate in %LocalAppData%
            // if not create the portable data folder
            // Don't create the folder if the version is 1.0.0 (Dev) to allow potential debugging with data in the project folder
            // It is still possible to create the UserData folder for dev version manually but we want to keep the current behavior
            if (!Constant.ProgramDirectory.IsSubPathOf(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
                && Constant.Version != "1.0.0")
            {
                Directory.CreateDirectory(DataLocation.PortableDataPath);
            }

            // Specify here so this method does not rely on other environment variables to initialise
            var portableDataDir = DataLocation.PortableDataPath;
            var roamingDataDir = DataLocation.RoamingDataPath;


            // Get full path to the .dead files for each case
            var portableDataDeleteFilePath = Path.Combine(portableDataDir, DataLocation.DeletionIndicatorFile);
            var roamingDataDeleteFilePath = Path.Combine(roamingDataDir, DataLocation.DeletionIndicatorFile);

            // If the data folder in %appdata% is marked for deletion,
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
