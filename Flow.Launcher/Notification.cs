using System;
using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using Flow.Launcher.Infrastructure;
using Microsoft.Toolkit.Uwp.Notifications;

namespace Flow.Launcher
{
    internal static class Notification
    {
        private static readonly string ClassName = nameof(Notification);

        internal static bool legacy = !Win32Helper.IsNotificationSupported();

        private static readonly ConcurrentDictionary<string, Action> _notificationActions = new();

        internal static void Install()
        {
            if (!legacy)
            {
                ToastNotificationManagerCompat.OnActivated += toastArgs =>
                {
                    var actionId = toastArgs.Argument; // Or use toastArgs.UserInput if using input
                    if (_notificationActions.TryGetValue(actionId, out var action))
                    {
                        action?.Invoke();
                    }
                };
            }
        }

        internal static void Uninstall()
        {
            if (!legacy)
            {
                _notificationActions.Clear();
                ToastNotificationManagerCompat.Uninstall();
            }
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

        public static void ShowWithButton(string title, string buttonText, Action buttonAction, string subTitle, string iconPath = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ShowInternalWithButton(title, buttonText, buttonAction, subTitle, iconPath);
            });
        }

        private static void ShowInternalWithButton(string title, string buttonText, Action buttonAction, string subTitle, string iconPath = null)
        {
            // Handle notification for win7/8/early win10
            if (legacy)
            {
                LegacyShowWithButton(title, buttonText, buttonAction, subTitle, iconPath);
                return;
            }

            // Using Windows Notification System
            var Icon = !File.Exists(iconPath)
                ? Path.Combine(Constant.ProgramDirectory, "Images\\app.png")
                : iconPath;

            try
            {
                var guid = Guid.NewGuid().ToString();
                new ToastContentBuilder()
                    .AddText(title, hintMaxLines: 1)
                    .AddText(subTitle)
                    .AddButton(buttonText, ToastActivationType.Background, guid)
                    .AddAppLogoOverride(new Uri(Icon))
                    .Show();
                _notificationActions.AddOrUpdate(guid, buttonAction, (key, oldValue) => buttonAction);
            }
            catch (InvalidOperationException e)
            {
                // Temporary fix for the Windows 11 notification issue
                // Possibly from 22621.1413 or 22621.1485, judging by post time of #2024
                App.API.LogException(ClassName, "Notification InvalidOperationException Error", e);
                LegacyShowWithButton(title, buttonText, buttonAction, subTitle, iconPath);
            }
            catch (Exception e)
            {
                App.API.LogException(ClassName, "Notification Error", e);
                LegacyShowWithButton(title, buttonText, buttonAction, subTitle, iconPath);
            }
        }

        private static void LegacyShowWithButton(string title, string buttonText, Action buttonAction, string subTitle, string iconPath)
        {
            var msg = new MsgWithButton();
            msg.Show(title, buttonText, buttonAction, subTitle, iconPath);
        }
    }
}
