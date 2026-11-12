using System;
using System.Linq;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Threading;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Avalonia;

// Search window placement across monitors, following the "Search Window Location" settings (port of the WPF
// MainWindow.InitializePosition). Positions are in physical pixels of the target screen.
public partial class MainWindow
{
    // Margin between the window and the screen edge for the top/left/right alignments, in device-independent pixels.
    private const double ScreenEdgeMargin = 10;

    /// <summary>
    /// Moves the window to the screen and alignment chosen in settings. Call before showing the window, while the
    /// previously active app is still frontmost (the "Focus" screen option looks at its window).
    /// </summary>
    private void PlaceOnSelectedScreen()
    {
        if (_settings == null)
        {
            return;
        }

        if (_settings.SearchWindowScreen == SearchWindowScreens.RememberLastLaunchLocation && TryRestoreLastPosition())
        {
            return;
        }

        if (SelectScreen() is { } screen)
        {
            Position = AlignOn(screen);
        }
    }

    /// <summary>
    /// Moves the window back to the screen and alignment chosen in settings, ignoring a remembered last position.
    /// </summary>
    public void ResetPosition()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ResetPosition);
            return;
        }

        if (_settings == null)
        {
            return;
        }

        if (SelectScreen() is { } screen)
        {
            Position = AlignOn(screen);
        }
    }

    /// <summary>
    /// Remembers where the window was when it hides, for the "Remember last launch location" option.
    /// </summary>
    private void SaveLastPosition()
    {
        if (_settings?.SearchWindowScreen != SearchWindowScreens.RememberLastLaunchLocation || !IsVisible)
        {
            return;
        }

        // Stored in device-independent pixels, like the WPF window's Left/Top.
        _settings.WindowLeft = Position.X / DesktopScaling;
        _settings.WindowTop = Position.Y / DesktopScaling;
    }

    private bool TryRestoreLastPosition()
    {
        var position = new PixelPoint(
            (int)Math.Round(_settings!.WindowLeft * DesktopScaling),
            (int)Math.Round(_settings.WindowTop * DesktopScaling));

        // The remembered spot can be off-screen after a monitor was disconnected; then use the cursor's screen.
        if (Screens.ScreenFromPoint(position) == null)
        {
            return false;
        }

        Position = position;
        return true;
    }

    private Screen? SelectScreen()
    {
        var screen = _settings!.SearchWindowScreen switch
        {
            SearchWindowScreens.Focus => GetForegroundWindowScreen(),
            SearchWindowScreens.Primary => Screens.Primary,
            SearchWindowScreens.Custom => _settings.CustomScreenNumber >= 1 && _settings.CustomScreenNumber <= Screens.All.Count
                ? Screens.All[_settings.CustomScreenNumber - 1]
                : Screens.All.FirstOrDefault(),
            // Cursor, and the fallback when the remembered location is gone
            _ => GetCursorScreen()
        };

        return screen ?? Screens.Primary ?? Screens.All.FirstOrDefault();
    }

    private PixelPoint AlignOn(Screen screen)
    {
        var area = screen.WorkingArea;
        var scaling = screen.Scaling;
        var width = (int)Math.Round((Bounds.Width > 0 ? Bounds.Width : Width) * scaling);
        var margin = (int)Math.Round(ScreenEdgeMargin * scaling);
        var centerX = area.X + (area.Width - width) / 2;
        var top = area.Y + margin;

        return _settings!.SearchWindowAlign switch
        {
            SearchWindowAligns.CenterTop => new PixelPoint(centerX, top),
            SearchWindowAligns.LeftTop => new PixelPoint(area.X + margin, top),
            SearchWindowAligns.RightTop => new PixelPoint(area.Right - width - margin, top),
            // Custom offsets are physical pixels from the working area's top-left corner, as in the WPF app.
            SearchWindowAligns.Custom => new PixelPoint(
                area.X + (int)Math.Round(_settings.CustomWindowLeft),
                area.Y + (int)Math.Round(_settings.CustomWindowTop)),
            // Center: horizontally centered, a quarter of the way down, measured with the query box height
            _ => new PixelPoint(centerX,
                area.Y + (int)Math.Round((area.Height - (_queryTextBox?.Bounds.Height ?? 0) * scaling) / 4))
        };
    }

    // Platform: the screen with the mouse cursor, and the screen of the window that was active before Flow showed.
    private partial Screen? GetCursorScreen();

    private partial Screen? GetForegroundWindowScreen();
}
