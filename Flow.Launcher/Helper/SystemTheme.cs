using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Management;
using System.Security.Principal;

namespace Flow.Launcher.Helper
{
    class SystemTheme
    {

        private const string PersonalizeKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
        private const string SysThemeValueName = "SystemUsesLightTheme";

        public static event EventHandler<SystemThemeChangedEventArgs> SystemThemeChanged;

        public static bool GetIsSystemLightTheme() => ReadDWord(SysThemeValueName);

        private static bool ReadDWord(string valueName)
        {
            var regkey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            if (regkey == null)
            {
                return false;
            }

            return (int)regkey.GetValue(valueName, 0) > 0;
        }

        public static void Initialize()
        {
            UpdateTheme();
            try
            {
                var currentUser = WindowsIdentity.GetCurrent();
                var query = new WqlEventQuery(string.Format("SELECT * FROM RegistryValueChangeEvent WHERE Hive='HKEY_USERS' AND KeyPath='{0}\\\\{1}' AND ValueName='{2}'",
                currentUser.User.Value, PersonalizeKey.Replace("\\", "\\\\"), SysThemeValueName));

                ManagementEventWatcher watcher = new(query);

                watcher.EventArrived += new EventArrivedEventHandler(HandleEvent);

                watcher.Start();
            }
            catch (ManagementException managementException)
            {
                Debug.WriteLine($"{nameof(SystemTheme)}: " + managementException.Message);
            }
        }

        private static void HandleEvent(object sender, EventArrivedEventArgs e)
        {
            UpdateTheme();
        }

        private static void UpdateTheme()
        {
            bool isSystemLightTheme = GetIsSystemLightTheme();
            var args = new SystemThemeChangedEventArgs(isSystemLightTheme);
            SystemThemeChanged?.Invoke(null, args);
        }
    }

    public class SystemThemeChangedEventArgs : EventArgs
    {
        public SystemThemeChangedEventArgs(bool isSystemLightTheme)
        {
            IsSystemLightTheme = isSystemLightTheme;
        }

        public bool IsSystemLightTheme { get; private set; }
    }
}
