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
}
