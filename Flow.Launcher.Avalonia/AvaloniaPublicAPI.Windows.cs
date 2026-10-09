using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Flow.Launcher.Avalonia.Views.Controls;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Avalonia;

public partial class AvaloniaPublicAPI
{
    partial void HookGlobalKeyboard()
    {
        Flow.Launcher.Infrastructure.Hotkey.GlobalHotkey.hookedKeyboardCallback = KListenerHookedKeyboardCallback;
    }

    private partial void StartShellCommand(string cmd, string filename) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = filename, Arguments = $"/c {cmd}", UseShellExecute = true });

    private partial bool IsSystemFileManager(string explorerPath) =>
        Path.GetFileNameWithoutExtension(explorerPath) == "explorer";

    private partial void OpenInSystemFileManager(string directoryPath, string? filePathToSelect)
    {
        if (filePathToSelect is null)
        {
            using var explorer = new Process();
            explorer.StartInfo = new ProcessStartInfo
            {
                FileName = directoryPath,
                UseShellExecute = true
            };
            explorer.Start();
        }
        else
        {
            Win32Helper.OpenFolderAndSelectFile(filePathToSelect);
        }
    }

    private partial ProcessStartInfo CreateFileManagerStartInfo(string fileName, string arguments, string targetPath) =>
        new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = true,
            Arguments = arguments
        };

    private partial void OpenInBrowser(string url, CustomBrowserViewModel browserInfo, string browserPath, bool inPrivate)
    {
        if (browserInfo.OpenInTab)
        {
            url.OpenInBrowserTab(browserPath, inPrivate, browserInfo.PrivateArg, browserInfo.ExtraArgs);
        }
        else
        {
            url.OpenInBrowserWindow(browserPath, inPrivate, browserInfo.PrivateArg, browserInfo.ExtraArgs);
        }
    }

    private partial bool OpenWpfPluginSettingsWindow(ISettingProvider settingProvider, PluginPair plugin)
    {
        var settingsControl = settingProvider.CreateSettingPanel();
        if (settingsControl == null)
        {
            return false;
        }

        WpfSettingsWindow.Show(settingsControl, plugin.Metadata.Name);
        return true;
    }

    private partial Task<Exception?> SetClipboardFileDropListAsync(string path) =>
        RetryActionOnStaThreadAsync(() =>
        {
            var paths = new StringCollection { path };
            Clipboard.SetFileDropList(paths);
        });

    private partial Task<Exception?> SetClipboardTextAsync(string text) =>
        RetryActionOnStaThreadAsync(() => Clipboard.SetText(text));

    private static async Task<Exception?> RetryActionOnStaThreadAsync(Action action, int retryCount = 6, int retryDelay = 150)
    {
        for (var i = 0; i < retryCount; i++)
        {
            try
            {
                await Win32Helper.StartSTATaskAsync(action).ConfigureAwait(false);
                return null;
            }
            catch (Exception e)
            {
                if (i == retryCount - 1)
                {
                    return e;
                }

                await Task.Delay(retryDelay).ConfigureAwait(false);
            }
        }

        return null;
    }

    public partial MessageBoxResult ShowMsgBox(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
    {
        if (System.Windows.Application.Current?.Dispatcher != null)
        {
            return System.Windows.Application.Current.Dispatcher.Invoke(() =>
                System.Windows.MessageBox.Show(messageBoxText, caption, button, icon, defaultResult));
        }

        return System.Windows.MessageBox.Show(messageBoxText, caption, button, icon, defaultResult);
    }

    public ValueTask<ImageSource> LoadImageAsync(string path, bool loadFullImage = false, bool cacheImage = true) =>
        Flow.Launcher.Infrastructure.Image.ImageLoader.LoadAsync(path, loadFullImage, cacheImage);
}
