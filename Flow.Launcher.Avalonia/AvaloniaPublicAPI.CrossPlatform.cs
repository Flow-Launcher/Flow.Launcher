using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Avalonia;

public partial class AvaloniaPublicAPI
{
    private const string OpenPath = "/usr/bin/open";

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

    // "cmd.exe" is the IPublicAPI default; here commands run through the user's login shell instead.
    private partial void StartShellCommand(string cmd, string filename)
    {
        var shell = string.Equals(filename, "cmd.exe", StringComparison.OrdinalIgnoreCase)
            ? Environment.GetEnvironmentVariable("SHELL") is { Length: > 0 } userShell ? userShell : "/bin/zsh"
            : filename;

        var startInfo = new ProcessStartInfo(shell) { UseShellExecute = false };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(cmd);
        Process.Start(startInfo)?.Dispose();
    }

    // Settings written on Windows keep "explorer" as the default entry; "Finder" is the macOS default entry.
    private partial bool IsSystemFileManager(string explorerPath) =>
        Path.GetFileNameWithoutExtension(explorerPath) is "explorer" or "finder";

    private partial void OpenInSystemFileManager(string directoryPath, string? filePathToSelect)
    {
        if (!OperatingSystem.IsMacOS())
        {
            Process.Start(new ProcessStartInfo { FileName = directoryPath, UseShellExecute = true })?.Dispose();
            return;
        }

        if (filePathToSelect is null)
        {
            RunOpen(directoryPath);
        }
        else
        {
            // Reveal (select) the item in Finder, like Explorer's /select.
            RunOpen("-R", filePathToSelect);
        }
    }

    private partial ProcessStartInfo CreateFileManagerStartInfo(string fileName, string arguments, string targetPath)
    {
        if (OperatingSystem.IsMacOS() && IsAppBundle(fileName))
        {
            // App bundles are not executables; hand the folder/file to the app as a document.
            var startInfo = new ProcessStartInfo(OpenPath) { UseShellExecute = false };
            startInfo.ArgumentList.Add("-a");
            startInfo.ArgumentList.Add(fileName);
            startInfo.ArgumentList.Add(targetPath);
            return startInfo;
        }

        return new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = true,
            Arguments = arguments
        };
    }

    private partial void OpenInBrowser(string url, CustomBrowserViewModel browserInfo, string browserPath, bool inPrivate)
    {
        if (!OperatingSystem.IsMacOS())
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true })?.Dispose();
            return;
        }

        if (string.IsNullOrWhiteSpace(browserPath))
        {
            // System default browser; it has no command-line private mode switch.
            RunOpen(url);
            return;
        }

        if (Path.IsPathRooted(browserPath) && !IsAppBundle(browserPath))
        {
            // A plain executable (e.g. a CLI wrapper); call it like Windows does.
            var direct = new ProcessStartInfo(browserPath) { UseShellExecute = false };
            AddBrowserArguments(direct.ArgumentList, url, browserInfo, inPrivate);
            Process.Start(direct)?.Dispose();
            return;
        }

        var app = ResolveMacBrowserApp(browserPath);
        var needsArguments = inPrivate || !browserInfo.OpenInTab || !string.IsNullOrWhiteSpace(browserInfo.ExtraArgs);
        if (!needsArguments)
        {
            RunOpen("-a", app, url);
            return;
        }

        // Launch arguments only reach the browser through "open -n ... --args"; the new instance forwards to a running one.
        var arguments = new List<string> { "-n", "-a", app, "--args" };
        AddBrowserArguments(arguments, url, browserInfo, inPrivate);
        RunOpen(arguments.ToArray());
    }

    private static void AddBrowserArguments(ICollection<string> arguments, string url, CustomBrowserViewModel browserInfo, bool inPrivate)
    {
        if (!browserInfo.OpenInTab)
        {
            arguments.Add("--new-window");
        }

        if (inPrivate && !string.IsNullOrWhiteSpace(browserInfo.PrivateArg))
        {
            arguments.Add(browserInfo.PrivateArg.Trim());
        }

        if (!string.IsNullOrWhiteSpace(browserInfo.ExtraArgs))
        {
            foreach (var extraArg in browserInfo.ExtraArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                arguments.Add(extraArg);
            }
        }

        arguments.Add(url);
    }

    // The built-in browser profiles use Windows executable names; map them to macOS application names.
    private static string ResolveMacBrowserApp(string browserPath)
    {
        if (IsAppBundle(browserPath))
        {
            return browserPath;
        }

        return Path.GetFileNameWithoutExtension(browserPath).ToLowerInvariant() switch
        {
            "chrome" => "Google Chrome",
            "firefox" => "Firefox",
            "msedge" => "Microsoft Edge",
            "brave" => "Brave Browser",
            _ => browserPath
        };
    }

    private static bool IsAppBundle(string path) =>
        path.TrimEnd('/').EndsWith(".app", StringComparison.OrdinalIgnoreCase);

    // Runs /usr/bin/open and surfaces its failure (e.g. unknown application) as an exception, since it reports errors only via exit code.
    private static void RunOpen(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(OpenPath)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new Win32Exception($"Failed to start {OpenPath}");
        var error = process.StandardError.ReadToEndAsync();
        _ = process.StandardOutput.ReadToEndAsync();

        // open returns as soon as Launch Services accepted the request; the timeout only guards against a hung service.
        if (!process.WaitForExit(TimeSpan.FromSeconds(10)))
        {
            return;
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"open {string.Join(' ', arguments)} failed ({process.ExitCode}): {error.GetAwaiter().GetResult().Trim()}");
        }
    }

    public partial MessageBoxResult ShowMsgBox(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
    {
        if (!OperatingSystem.IsMacOS())
        {
            Log.Warn(nameof(AvaloniaPublicAPI), $"Message box is not supported on this platform: {caption}: {messageBoxText}");
            return MessageBoxResult.None;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            return ShowMacDialog(messageBoxText, caption, button, icon, defaultResult);
        }

        // osascript blocks until answered. Like WPF's MessageBox, keep pumping the UI thread (nested frame)
        // while the dialog is open so Flow's windows keep rendering; callers still get a synchronous result.
        var dialogTask = Task.Run(() => ShowMacDialog(messageBoxText, caption, button, icon, defaultResult));
        var frame = new DispatcherFrame();
        dialogTask.ContinueWith(_ => Dispatcher.UIThread.Post(() => frame.Continue = false), TaskScheduler.Default);
        try
        {
            Dispatcher.UIThread.PushFrame(frame);
        }
        catch (InvalidOperationException e)
        {
            // Dispatcher processing is suspended (e.g. during layout); fall back to a plain blocking wait.
            Log.Warn(nameof(AvaloniaPublicAPI), $"Cannot push a dispatcher frame for the message box: {e.Message}");
        }

        return dialogTask.GetAwaiter().GetResult();
    }

    private MessageBoxResult ShowMacDialog(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
    {
        // Buttons are listed left to right; macOS places the affirmative button last (rightmost).
        // The first entry doubles as the cancel button (Esc / "User canceled") when the dialog can be declined.
        MessageBoxResult[] results = button switch
        {
            MessageBoxButton.OKCancel => [MessageBoxResult.Cancel, MessageBoxResult.OK],
            MessageBoxButton.YesNo => [MessageBoxResult.No, MessageBoxResult.Yes],
            MessageBoxButton.YesNoCancel => [MessageBoxResult.Cancel, MessageBoxResult.No, MessageBoxResult.Yes],
            _ => [MessageBoxResult.OK]
        };
        var cancelResult = results.Length > 1 ? results[0] : MessageBoxResult.None;
        var defaultIndex = Array.IndexOf(results, defaultResult);
        if (defaultIndex < 0)
        {
            defaultIndex = results.Length - 1;
        }

        var labels = Array.ConvertAll(results, GetButtonLabel);

        // argv: 1 = text, 2 = title, 3.. = button labels. Passing text as arguments avoids AppleScript string escaping.
        var buttonItems = new List<string>();
        for (var i = 0; i < labels.Length; i++)
        {
            buttonItems.Add($"item {i + 3} of argv");
        }

        var dialog = $"display dialog (item 1 of argv) with title (item 2 of argv) buttons {{{string.Join(", ", buttonItems)}}} default button {defaultIndex + 1}";
        if (cancelResult != MessageBoxResult.None)
        {
            dialog += " cancel button 1";
        }

        var iconName = GetDialogIconName(icon);
        if (iconName != null)
        {
            dialog += $" with icon {iconName}";
        }

        var arguments = new List<string> { messageBoxText ?? string.Empty, string.IsNullOrEmpty(caption) ? Constant.FlowLauncherFullName : caption };
        arguments.AddRange(labels);

        try
        {
            // "activate" brings the osascript dialog to the front instead of opening behind other apps.
            var result = AppleScript.Run(["activate", dialog], arguments.ToArray());
            if (result.ExitCode == AppleScript.UserCanceledExitCode)
            {
                return cancelResult;
            }

            if (result.ExitCode != 0)
            {
                Log.Error(nameof(AvaloniaPublicAPI), $"osascript dialog failed ({result.ExitCode}): {result.Error}");
                return MessageBoxResult.None;
            }

            var clicked = ParseButtonReturned(result.Output);
            var index = Array.IndexOf(labels, clicked);
            return index >= 0 ? results[index] : MessageBoxResult.None;
        }
        catch (Exception e)
        {
            Log.Exception(nameof(AvaloniaPublicAPI), "Failed to show message box", e);
            return MessageBoxResult.None;
        }
    }

    // osascript prints the dialog record as "button returned:Yes" (plus ", gave up:false" when a timeout is set).
    private static string ParseButtonReturned(string output)
    {
        const string prefix = "button returned:";
        var start = output.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        start += prefix.Length;
        var end = output.IndexOf(", gave up:", start, StringComparison.Ordinal);
        return end < 0 ? output[start..] : output[start..end];
    }

    private string GetButtonLabel(MessageBoxResult result)
    {
        var (key, fallback) = result switch
        {
            MessageBoxResult.Cancel => ("commonCancel", "Cancel"),
            MessageBoxResult.Yes => ("commonYes", "Yes"),
            MessageBoxResult.No => ("commonNo", "No"),
            _ => ("commonOK", "OK")
        };

        return _i18n.HasTranslation(key) ? _i18n.GetTranslation(key) : fallback;
    }

    private static string? GetDialogIconName(MessageBoxImage icon) => icon switch
    {
        MessageBoxImage.Error => "stop",
        MessageBoxImage.Warning => "caution",
        MessageBoxImage.Information or MessageBoxImage.Question => "note",
        _ => null
    };
}
