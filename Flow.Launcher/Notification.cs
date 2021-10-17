using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Image;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Flow.Launcher
{
    internal static class Notification
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
        public static void Show(string title, string subTitle, string iconPath)
        {
            /* Using Windows Notification System */
            var Icon = !File.Exists(iconPath)
                ? ImageLoader.Load(Path.Combine(Constant.ProgramDirectory, "Images\\app.png"))
                : ImageLoader.Load(iconPath);

            var xml = $"<?xml version=\"1.0\"?><toast><visual><binding template=\"ToastImageAndText04\"><image id=\"1\" src=\"{Icon}\" alt=\"meziantou\"/><text id=\"1\">{title}</text>" +
                $"<text id=\"2\">{subTitle}</text></binding></visual></toast>";
            var toastXml = new XmlDocument();
            toastXml.LoadXml(xml);
            var toast = new ToastNotification(toastXml);
            ToastNotificationManager.CreateToastNotifier("Flow Launcher").Show(toast);

        }
    }
}
