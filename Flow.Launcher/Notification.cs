using Flow.Launcher.Infrastructure;
using System;
using System.IO;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Flow.Launcher
{
    internal static class Notification
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
        public static void Show(string title, string subTitle, string iconPath)
        {
            var legacy = Environment.OSVersion.Version.Build < 19041;
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

            var xml = $"<?xml version=\"1.0\"?><toast><visual><binding template=\"ToastImageAndText04\"><image id=\"1\" src=\"{Icon}\" alt=\"meziantou\"/><text id=\"1\">{title}</text>" +
                      $"<text id=\"2\">{subTitle}</text></binding></visual></toast>";
            var toastXml = new XmlDocument();
            toastXml.LoadXml(xml);
            var toast = new ToastNotification(toastXml);
            ToastNotificationManager.CreateToastNotifier("Flow Launcher").Show(toast);

        }

        private static void LegacyShow(string title, string subTitle, string iconPath)
        {
            var msg = new Msg();
            msg.Show(title, subTitle, iconPath);
        }
    }
}