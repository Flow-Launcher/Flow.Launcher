using Flow.Launcher.Infrastructure;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.IO;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Flow.Launcher
{
    internal static class Notification
    {
        internal static bool legacy = Environment.OSVersion.Version.Build < 19041;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
        internal static void Uninstall()
        {
            if (!legacy)
                ToastNotificationManagerCompat.Uninstall();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
        public static void Show(string title, string subTitle, string iconPath)
        {
            // Handle notification for win7/8/early win10
            if (legacy)
            {
                LegacyShow(title, subTitle, iconPath);
                return;
            }

            // Using Windows Notification System
            var Icon = !File.Exists(iconPath)
                ? Path.Combine(Constant.ProgramDirectory, "Images\\app.png")
                : iconPath;

            new ToastContentBuilder()
                .AddText(title, hintMaxLines: 1)
                .AddText(subTitle)
                .AddAppLogoOverride(new Uri(Icon))
                .Show();
        }

        private static void LegacyShow(string title, string subTitle, string iconPath)
        {
            var msg = new Msg();
            msg.Show(title, subTitle, iconPath);
        }
    }
}