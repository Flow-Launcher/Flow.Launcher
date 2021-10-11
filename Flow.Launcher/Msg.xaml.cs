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
using Notification.Wpf;
using Notification.Wpf.Converters;

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
            var notificationManager = new NotificationManager();

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
            else {
                imgIco.Source = ImageLoader.Load(iconPath);
            }

            var content = new NotificationContent
            {
                Title = title,
                Message = subTitle,
                Type = NotificationType.Notification,
                CloseOnClick = true, // Set true if u want close message when left mouse button click on message (base = true)

                Background = new SolidColorBrush(Colors.Black),
                Foreground = new SolidColorBrush(Colors.White),
                Icon = imgIco.Source
            };
           
            notificationManager.Show(content);
        }

    }
}
