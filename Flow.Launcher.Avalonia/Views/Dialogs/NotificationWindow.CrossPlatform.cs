using System;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Flow.Launcher.Avalonia.Helper;

namespace Flow.Launcher.Avalonia.Views.Dialogs;

public partial class NotificationWindow
{
    // The native blur fills the square window; clip it to the card's rounded corners and keep the window background
    // clear whenever Avalonia (re)applies the blur, otherwise a grey rectangle shows around the card.
    partial void InitializeWindowShape()
    {
        if (!OperatingSystem.IsMacOS() || this.FindControl<Border>("NotificationBorder") is not { } border)
        {
            return;
        }

        MacWindowShape.SetCornerRadius(this, border.CornerRadius);
        MacWindowShape.ClearBackgroundIfTransparent(this);
        base.PropertyChanged += (_, e) =>
        {
            if (e.Property == ActualTransparencyLevelProperty)
            {
                MacWindowShape.ClearBackgroundIfTransparent(this);
            }
        };
    }

    // X11: tiling window managers float utility windows and, unlike dialogs, do not focus them when they appear.
    // Wayland: a dedicated app id, so compositor rules can float, place and not focus notifications (docs/linux.md).
    partial void InitializeWindowType()
    {
        if (OperatingSystem.IsLinux())
        {
            X11Properties.SetNetWmWindowType(this, X11NetWmWindowType.Utility);
            WaylandProperties.SetAppId(this, Program.LinuxAppId + ".Notification");
        }
    }

    // Linux: the desktop's notification server; Flow's own window only when none is reachable.
    static partial void ShowPlatformNotification(string title, string subTitle, string iconPath, string? actionButtonText,
        Action? buttonAction, ref bool handled)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        handled = true;
        _ = ShowThroughServerAsync(title, subTitle, iconPath, actionButtonText, buttonAction);
    }

    private static async System.Threading.Tasks.Task ShowThroughServerAsync(string title, string subTitle, string iconPath,
        string? actionButtonText, Action? buttonAction)
    {
        if (!await LinuxDesktopNotifications.TryShowAsync(title, subTitle, iconPath, actionButtonText, buttonAction))
        {
            ShowWindow(title, subTitle, iconPath, actionButtonText, buttonAction);
        }
    }
}
