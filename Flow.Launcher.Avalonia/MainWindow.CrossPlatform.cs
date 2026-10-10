using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Flow.Launcher.Avalonia.Helper;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Avalonia;

public partial class MainWindow
{
    // Window.Activate() only makes the window key inside this app; macOS also needs the app itself activated
    // when the launcher is summoned by a global hotkey while another app is frontmost.
    partial void ActivateApplication()
    {
        if (OperatingSystem.IsMacOS())
        {
            MacWindowActivation.ActivateForWindow(this);
        }
    }

    // A hidden window leaves the app active with no window to focus; hide the app so the previous app gets focus
    // back, unless another Flow Launcher window (e.g. settings) is still open.
    partial void ReturnFocusAfterHide()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.Windows.Any(window => window != this && window.IsVisible))
        {
            return;
        }

        MacWindowActivation.ReturnFocusToPreviousApp();
    }

    // The native blur fills the square NSWindow; clip it to WindowBorder's corners (the style and theme switches
    // change CornerRadius) and keep the window background clear whenever Avalonia (re)applies the blur.
    partial void InitializeWindowShape()
    {
        if (!OperatingSystem.IsMacOS() || this.FindControl<Border>("WindowBorder") is not { } border)
        {
            return;
        }

        MacWindowShape.SetCornerRadius(this, border.CornerRadius);
        MacWindowShape.ClearBackgroundIfTransparent(this);

        border.PropertyChanged += (_, e) =>
        {
            if (e.Property == Border.CornerRadiusProperty)
            {
                MacWindowShape.SetCornerRadius(this, border.CornerRadius);
            }
        };
        PropertyChanged += (_, e) =>
        {
            if (e.Property == ActualTransparencyLevelProperty)
            {
                MacWindowShape.ClearBackgroundIfTransparent(this);
            }
        };
    }

    // The WPF app plays Resources/open.wav through SoundPlayer/MediaPlayer; macOS plays it with afplay, started in
    // the background so showing the window never waits for the sound.
    partial void PlayOpenSound()
    {
        if (!OperatingSystem.IsMacOS() || _settings == null)
        {
            return;
        }

        var soundPath = Path.Combine(AppContext.BaseDirectory, "Resources", "open.wav");
        if (!File.Exists(soundPath))
        {
            return;
        }

        var volume = Math.Clamp(_settings.SoundVolume / 100.0, 0.0, 1.0);
        try
        {
            var startInfo = new ProcessStartInfo("/usr/bin/afplay")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-v");
            startInfo.ArgumentList.Add(volume.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add(soundPath);

            // Disposing the handle does not stop afplay.
            using var process = Process.Start(startInfo);
        }
        catch (Exception e)
        {
            Log.Exception(ClassName, "Failed to play the open sound", e);
        }
    }

    private partial Screen? GetCursorScreen() =>
        OperatingSystem.IsMacOS() && MacScreens.TryGetCursorLocation(out var x, out var y) ? ScreenAtMacPoint(x, y) : null;

    private partial Screen? GetForegroundWindowScreen() =>
        OperatingSystem.IsMacOS() && MacScreens.TryGetFrontmostWindowCenter(out var x, out var y) ? ScreenAtMacPoint(x, y) : null;

    // CoreGraphics reports global points; Avalonia reports each screen's bounds in that screen's pixels (points × scaling).
    private Screen? ScreenAtMacPoint(double x, double y) =>
        Screens.All.FirstOrDefault(screen =>
        {
            var bounds = screen.Bounds.ToRect(screen.Scaling);
            return bounds.Contains(new Point(x, y));
        });
}
