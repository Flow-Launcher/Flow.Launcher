using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Image;
using Windows.UI;
using Windows.Data;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Flow.Launcher
{
    public partial class Msg : Window
    {

        public Msg()
        {
            InitializeComponent();

        }

        public void Show(string title, string subTitle, string iconPath)
        {


            tbTitle.Text = title;
            tbSubTitle.Text = subTitle;
            if (string.IsNullOrEmpty(subTitle))
            {
                tbSubTitle.Visibility = Visibility.Collapsed;
            }
            if (!File.Exists(iconPath))
            {
                imgIco.Source = ImageLoader.Load(Path.Combine(Constant.ProgramDirectory, "Images\\app.png"));
            }
            else
            {
                imgIco.Source = ImageLoader.Load(iconPath);
            }

            /* Using Windows Notification System */
            var ToastTitle = title;
            var ToastsubTitle = subTitle;
            var Icon = imgIco.Source;
            var xml = $"<?xml version=\"1.0\"?><toast><visual><binding template=\"ToastImageAndText04\"><image id=\"1\" src=\"{Icon}\" alt=\"meziantou\"/><text id=\"1\">{ToastTitle}</text>" +
                $"<text id=\"2\">{ToastsubTitle}</text></binding></visual></toast>";
            var toastXml = new XmlDocument();
            toastXml.LoadXml(xml);
            var toast = new ToastNotification(toastXml);
            ToastNotificationManager.CreateToastNotifier("Flow Launcher").Show(toast);

        }

    }
}
