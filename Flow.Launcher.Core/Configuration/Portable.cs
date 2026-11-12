using System;
using System.IO;
using System.Windows;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Core.Configuration
{
    public partial class Portable : IPortable
    {
        private static readonly string ClassName = nameof(Portable);

        public void MoveUserDataFolder(string fromLocation, string toLocation)
        {
            FilesFolders.CopyAll(fromLocation, toLocation, (s) => PublicApi.Instance.ShowMsgBox(s));
            VerifyUserDataAfterMove(fromLocation, toLocation);
        }

        public void VerifyUserDataAfterMove(string fromLocation, string toLocation)
        {
            FilesFolders.VerifyBothFolderFilesEqual(fromLocation, toLocation, (s) => PublicApi.Instance.ShowMsgBox(s));
        }

        private static void IndicateDeletion(string filePathTodelete)
        {
            var deleteFilePath = Path.Combine(filePathTodelete, DataLocation.DeletionIndicatorFile);
            using var _ = File.CreateText(deleteFilePath);
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

            // If the data folder in %appdata% is marked for deletion,
            // delete it and prompt the user to pick the portable data location
            if (File.Exists(roamingDataDeleteFilePath))
            {
                FilesFolders.RemoveFolderIfExists(roamingDataDir, (s) => PublicApi.Instance.ShowMsgBox(s));

                if (PublicApi.Instance.ShowMsgBox(Localize.moveToDifferentLocation(), 
                    string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    FilesFolders.OpenPath(Constant.RootDirectory, (s) => PublicApi.Instance.ShowMsgBox(s));

                    Environment.Exit(0);
                }
            }
            // Otherwise, if the portable data folder is marked for deletion,
            // delete it and notify the user about it.
            else if (File.Exists(portableDataDeleteFilePath))
            {
                FilesFolders.RemoveFolderIfExists(portableDataDir, (s) => PublicApi.Instance.ShowMsgBox(s));

                PublicApi.Instance.ShowMsgBox(Localize.shortcutsUninstallerCreated());
            }
        }

        public bool CanUpdatePortability()
        {
            var roamingLocationExists = DataLocation.RoamingDataPath.LocationExists();
            var portableLocationExists = DataLocation.PortableDataPath.LocationExists();

            if (roamingLocationExists && portableLocationExists)
            {
                PublicApi.Instance.ShowMsgBox(Localize.userDataDuplicated(DataLocation.PortableDataPath, DataLocation.RoamingDataPath, Environment.NewLine));

                return false;
            }

            return true;
        }
    }
}
