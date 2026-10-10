using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Flow.Launcher.Plugin.Url
{
#pragma warning disable FLAN0005 // The plugin context property is declared in Main.cs
    public partial class Main
#pragma warning restore FLAN0005
    {
        // Opens the url with the configured browser. On macOS BrowserPath may be an app name or an .app bundle path
        // and is launched through /usr/bin/open; elsewhere it must be an executable.
        private partial void OpenInCustomBrowser(string url)
        {
            var browserArgs = new List<string>();
            if (Settings.OpenInNewBrowserWindow)
                browserArgs.Add("--new-window");
            if (Settings.OpenInPrivateMode)
                browserArgs.AddRange(Settings.PrivateModeArgument.Split(' ', StringSplitOptions.RemoveEmptyEntries));

            var psi = new ProcessStartInfo { UseShellExecute = false };
            if (OperatingSystem.IsMacOS())
            {
                psi.FileName = "/usr/bin/open";
                if (browserArgs.Count == 0)
                {
                    psi.ArgumentList.Add("-a");
                    psi.ArgumentList.Add(Settings.BrowserPath);
                    psi.ArgumentList.Add(url);
                }
                else
                {
                    // Command line arguments only reach the browser when open launches a new instance;
                    // Chromium/Firefox forward them to an already running instance.
                    psi.ArgumentList.Add("-n");
                    psi.ArgumentList.Add("-a");
                    psi.ArgumentList.Add(Settings.BrowserPath);
                    psi.ArgumentList.Add("--args");
                    foreach (var arg in browserArgs)
                        psi.ArgumentList.Add(arg);
                    psi.ArgumentList.Add(url);
                }
            }
            else
            {
                psi.FileName = Settings.BrowserPath;
                foreach (var arg in browserArgs)
                    psi.ArgumentList.Add(arg);
                psi.ArgumentList.Add(url);
            }

            using var process = Process.Start(psi);
            if (OperatingSystem.IsMacOS())
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"open failed to launch {Settings.BrowserPath} (exit code {process.ExitCode})");
            }
        }
    }
}
