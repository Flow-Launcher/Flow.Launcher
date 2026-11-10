using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Platform;
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

    // A normal top-level window is tiled by tiling window managers (Hyprland, Sway and i3 via XWayland/X11).
    // _NET_WM_WINDOW_TYPE_UTILITY makes them float it at the position Flow chooses, and stacking desktops
    // (GNOME, KDE) keep treating it as a borderless, taskbar-less panel.
    partial void InitializeWindowType()
    {
        if (OperatingSystem.IsLinux())
        {
            X11Properties.SetNetWmWindowType(this, X11NetWmWindowType.Dialog);
        }
    }

    // The WPF app plays Resources/open.wav through SoundPlayer/MediaPlayer; macOS plays it with afplay and Linux with
    // the first of pw-play (PipeWire), paplay (PulseAudio) and aplay (ALSA, no volume control) on PATH, started in the
    // background so showing the window never waits for the sound.
    partial void PlayOpenSound()
    {
        if (!(OperatingSystem.IsMacOS() || OperatingSystem.IsLinux()) || _settings == null)
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
            var startInfo = OperatingSystem.IsMacOS() ? new ProcessStartInfo("/usr/bin/afplay") : CreateLinuxSoundStartInfo(volume);
            if (startInfo is null)
            {
                return;
            }

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            if (OperatingSystem.IsMacOS())
            {
                startInfo.ArgumentList.Add("-v");
                startInfo.ArgumentList.Add(volume.ToString(CultureInfo.InvariantCulture));
            }

            startInfo.ArgumentList.Add(soundPath);

            // Disposing the handle does not stop the player.
            using var process = Process.Start(startInfo);
        }
        catch (Exception e)
        {
            Log.Exception(ClassName, "Failed to play the open sound", e);
        }
    }

    private static ProcessStartInfo? CreateLinuxSoundStartInfo(double volume)
    {
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        string? Find(string name) => directories.Select(d => Path.Combine(d, name)).FirstOrDefault(File.Exists);

        if (Find("pw-play") is { } pwPlay)
        {
            var startInfo = new ProcessStartInfo(pwPlay);
            startInfo.ArgumentList.Add($"--volume={volume.ToString(CultureInfo.InvariantCulture)}");
            return startInfo;
        }

        if (Find("paplay") is { } paplay)
        {
            var startInfo = new ProcessStartInfo(paplay);
            startInfo.ArgumentList.Add($"--volume={(int)Math.Round(volume * 65536)}");
            return startInfo;
        }

        if (Find("aplay") is { } aplay)
        {
            var startInfo = new ProcessStartInfo(aplay);
            startInfo.ArgumentList.Add("--quiet");
            return startInfo;
        }

        Log.Warn(ClassName, "No sound player (pw-play, paplay, aplay) found; the open sound is not played");
        return null;
    }

    private partial Screen? GetCursorScreen()
    {
        if (OperatingSystem.IsMacOS())
        {
            return MacScreens.TryGetCursorLocation(out var x, out var y) ? ScreenAtMacPoint(x, y) : null;
        }

        // X11 root coordinates and Avalonia's X11 screen bounds are both physical pixels.
        return OperatingSystem.IsLinux() && LinuxGlobalHotkey.TryGetPointerPosition(out var px, out var py)
            ? Screens.All.FirstOrDefault(screen => screen.Bounds.Contains(new PixelPoint(px, py)))
            : null;
    }

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
