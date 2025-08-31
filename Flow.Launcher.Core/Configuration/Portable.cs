using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;
using Microsoft.Win32;
using Squirrel;

namespace Flow.Launcher.Core.Configuration
{
    public class Portable : IPortable
    {
        private static readonly string ClassName = nameof(Portable);

        private readonly IPublicAPI API = Ioc.Default.GetRequiredService<IPublicAPI>();

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

                API.ShowMsgBox(API.GetTranslation("restartToDisablePortableMode"));

                UpdateManager.RestartApp(Constant.ApplicationFileName);
            }
            catch (Exception e)
            {
                API.LogException(ClassName, "Error occurred while disabling portable mode", e);
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

                API.ShowMsgBox(API.GetTranslation("restartToEnablePortableMode"));

                UpdateManager.RestartApp(Constant.ApplicationFileName);
            }
            catch (Exception e)
            {
                API.LogException(ClassName, "Error occurred while enabling portable mode", e);
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

        public void MoveUserDataFolder(string fromLocation, string toLocation)
        {
            FilesFolders.CopyAll(fromLocation, toLocation, (s) => API.ShowMsgBox(s));
            VerifyUserDataAfterMove(fromLocation, toLocation);
        }

        public void VerifyUserDataAfterMove(string fromLocation, string toLocation)
        {
            FilesFolders.VerifyBothFolderFilesEqual(fromLocation, toLocation, (s) => API.ShowMsgBox(s));
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
            var portableDataDir = Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location.NonNull()).ToString(), "UserData");
            var roamingDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlowLauncher");

            // Get full path to the .dead files for each case
            var portableDataDeleteFilePath = Path.Combine(portableDataDir, DataLocation.DeletionIndicatorFile);
            var roamingDataDeleteFilePath = Path.Combine(roamingDataDir, DataLocation.DeletionIndicatorFile);

            // If the data folder in %appdata% is marked for deletion,
            // delete it and prompt the user to pick the portable data location
            if (File.Exists(roamingDataDeleteFilePath))
            {
                FilesFolders.RemoveFolderIfExists(roamingDataDir, (s) => API.ShowMsgBox(s));

                if (API.ShowMsgBox(API.GetTranslation("moveToDifferentLocation"), 
                    string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    FilesFolders.OpenPath(Constant.RootDirectory, (s) => API.ShowMsgBox(s));

                    Environment.Exit(0);
                }
            }
            // Otherwise, if the portable data folder is marked for deletion,
            // delete it and notify the user about it.
            else if (File.Exists(portableDataDeleteFilePath))
            {
                FilesFolders.RemoveFolderIfExists(portableDataDir, (s) => API.ShowMsgBox(s));

                API.ShowMsgBox(API.GetTranslation("shortcutsUninstallerCreated"));
            }
        }

        public bool CanUpdatePortability()
        {
            var roamingLocationExists = DataLocation.RoamingDataPath.LocationExists();
            var portableLocationExists = DataLocation.PortableDataPath.LocationExists();

            if (roamingLocationExists && portableLocationExists)
            {
                API.ShowMsgBox(string.Format(API.GetTranslation("userDataDuplicated"), 
                    DataLocation.PortableDataPath, DataLocation.RoamingDataPath, Environment.NewLine));

                return false;
            }

            return true;
        }
    }
}
