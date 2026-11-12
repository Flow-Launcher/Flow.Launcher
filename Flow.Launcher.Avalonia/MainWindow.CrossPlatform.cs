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

    /// <summary>
    /// Wayland app id of the search window. It differs from the app's id (used by the settings and other windows) so
    /// compositor rules can float exactly this window; matching on the title does not work there, because rules are
    /// evaluated before the title is set and the compositor's tiled size would then leak into the window width.
    /// </summary>
    internal const string LinuxSearchWindowAppId = Program.LinuxAppId + ".Search";

    // Tiling window managers tile normal top-level windows. On X11 (XWayland), _NET_WM_WINDOW_TYPE_DIALOG makes them
    // float and focus the search window at the position Flow chooses; stacking desktops (GNOME, KDE) keep treating
    // it as a borderless panel. Wayland has no window types and clients cannot position themselves, so there the
    // compositor places it, and tiling compositors need a window rule on its app id (docs/linux.md).
    partial void InitializeWindowType()
    {
        if (OperatingSystem.IsLinux())
        {
            X11Properties.SetNetWmWindowType(this, X11NetWmWindowType.Dialog);
            WaylandProperties.SetAppId(this, LinuxSearchWindowAppId);

            if (Program.UsesNativeWayland && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE"))
                && this.FindControl<Border>("WindowBorder") is { } border)
            {
                ReserveFullHeight(border);
            }
        }
    }

    // Hyprland keeps a floating Wayland window at the size it had when it was mapped and ignores the client's later
    // size changes (it only clamps to the min/max size hints). A search window that sizes to its results would be
    // cut off when the list grows. So on Hyprland the window always has the height of the longest possible list
    // (or of the preview panel, whichever is taller), also pinned through the min/max hints; the visible border
    // stays top-aligned at the content's height and the rest of the window is transparent (docs/linux.md has the
    // rule that keeps Hyprland from framing it). Window.MinHeight alone would only set the hints: a top-level
    // window's layout ignores its own min/max, so the height is set explicitly instead of sizing to content.
    private void ReserveFullHeight(Border windowBorder)
    {
        windowBorder.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top;
        LayoutUpdated += (_, _) =>
        {
            var height = FullContentHeight(windowBorder);
            if (height <= 0)
            {
                return;
            }

            // A finished drag-resize switches back to sizing to content (ApplyUserResize).
            if (SizeToContent != SizeToContent.Manual)
            {
                SizeToContent = SizeToContent.Manual;
            }

            if (Math.Abs(Height - height) >= 0.5 || Math.Abs(MinHeight - height) >= 0.5 || Math.Abs(MaxHeight - height) >= 0.5)
            {
                MinHeight = height;
                MaxHeight = height;
                Height = height;
            }
        };
    }

    // Height of WindowBorder with the result list at its maximum: the always-visible query box plus the vertical
    // margins, borders and paddings of the elements around the list, plus the list's MaxHeight.
    private double FullContentHeight(Border windowBorder)
    {
        if (_viewModel == null
            || this.FindControl<Control>("QueryBoxArea") is not { } queryBoxArea
            || this.FindControl<Control>("MiddleSeparator") is not { } separator
            || this.FindControl<Border>("ResultAreaBorder") is not { } resultAreaBorder
            || this.FindControl<Control>("ResultArea") is not { } resultArea
            || this.FindControl<Control>("ResultListBox") is not { } resultListBox
            || this.FindControl<Control>("PreviewPanelContainer") is not { } previewContainer)
        {
            return 0;
        }

        static double Outer(Thickness margin) => margin.Top + margin.Bottom;
        static double Inner(Border border) => border.BorderThickness.Top + border.BorderThickness.Bottom
                                              + border.Padding.Top + border.Padding.Bottom;

        var list = Outer(resultArea.Margin) + Outer(resultListBox.Margin) + _viewModel.Results.MaxHeight;
        var preview = Outer(previewContainer.Margin) + previewContainer.MinHeight;

        var height = Outer(windowBorder.Margin) + Inner(windowBorder)
                     + queryBoxArea.DesiredSize.Height
                     + Outer(separator.Margin) + (double.IsNaN(separator.Height) ? 0 : separator.Height)
                     + Outer(resultAreaBorder.Margin) + Inner(resultAreaBorder)
                     + Math.Max(list, preview);
        return Math.Ceiling(height);
    }

    // Avalonia's Wayland backend recreates the toplevel on every Show and commits it under the app-wide app id before
    // applying the window's own one, so the compositor's first configure suggests the size of a normal (tiled) window
    // and Avalonia adopts its width. Put the configured width back once; a tiling compositor still has the last word.
    partial void RestoreWidthAfterShow()
    {
        if (OperatingSystem.IsLinux() && _settings != null && Math.Abs(Width - _settings.WindowSize) >= 1)
        {
            SetCurrentValue(WidthProperty, _settings.WindowSize);
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
