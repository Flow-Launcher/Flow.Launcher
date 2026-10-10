using System;
using Avalonia.Controls;
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
}
