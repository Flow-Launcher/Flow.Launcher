using System;
using System.Threading.Tasks;
using System.Windows;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Avalonia;

public partial class AvaloniaPublicAPI
{
    // No WPF host here, so legacy WPF settings panels cannot be shown.
    private partial bool OpenWpfPluginSettingsWindow(ISettingProvider settingProvider, PluginPair plugin) => false;

    // Copying files as a file-drop list is not implemented for this platform yet.
    private partial Task<Exception?> SetClipboardFileDropListAsync(string path) =>
        Task.FromResult<Exception?>(new PlatformNotSupportedException("Copying files to the clipboard is not supported on this platform."));

    private partial async Task<Exception?> SetClipboardTextAsync(string text)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var clipboard = GetMainWindowClipboard()
                    ?? throw new InvalidOperationException("Main window clipboard is not available.");
                await clipboard.SetTextAsync(text);
            });
            return null;
        }
        catch (Exception e)
        {
            return e;
        }
    }

    private static IClipboard? GetMainWindowClipboard() =>
        (global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Clipboard;

    // There is no Avalonia message box in this app yet; report "no answer" so callers take their negative path.
    public partial MessageBoxResult ShowMsgBox(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
    {
        Log.Warn(nameof(AvaloniaPublicAPI), $"Message box is not supported on this platform: {caption}: {messageBoxText}");
        return MessageBoxResult.None;
    }
}
