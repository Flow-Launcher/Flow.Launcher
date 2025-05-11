using Flow.Launcher.Infrastructure;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.IO;
using System.Windows;

namespace Flow.Launcher
{
    internal static class Notification
    {
        private static readonly string ClassName = nameof(Notification);

        internal static bool legacy = !Win32Helper.IsNotificationSupported();

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
                App.API.LogException(ClassName, "Notification InvalidOperationException Error", e);
                LegacyShow(title, subTitle, iconPath);
            }
            catch (Exception e)
            {
                App.API.LogException(ClassName, "Notification Error", e);
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
