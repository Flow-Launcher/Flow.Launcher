using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.IO;
using System.Windows;

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

        public static void Show(string title, string subTitle, string iconPath = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ShowInternal(title, subTitle, iconPath);
            });
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
        private static void ShowInternal(string title, string subTitle, string iconPath = null)
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

            try
            {
                new ToastContentBuilder()
                    .AddText(title, hintMaxLines: 1)
                    .AddText(subTitle)
                    .AddAppLogoOverride(new Uri(Icon))
                    .Show();
            }
            catch (InvalidOperationException e)
            {
                // Temporary fix for the Windows 11 notification issue
                // Possibly from 22621.1413 or 22621.1485, judging by post time of #2024
                Log.Exception("Flow.Launcher.Notification|Notification InvalidOperationException Error", e);
                LegacyShow(title, subTitle, iconPath);
            }
            catch (Exception e)
            {
                Log.Exception("Flow.Launcher.Notification|Notification Error", e);
                LegacyShow(title, subTitle, iconPath);
            }
        }

        private static void LegacyShow(string title, string subTitle, string iconPath)
        {
            var msg = new Msg();
            msg.Show(title, subTitle, iconPath);
        }
    }
}
