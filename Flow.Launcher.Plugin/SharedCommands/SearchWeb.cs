using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Flow.Launcher.Plugin.SharedCommands
{
    public static class SearchWeb
    {
        private static string GetDefaultBrowserPath()
        {
            string name = string.Empty;
            try
            {
                using var regDefault = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\Shell\\Associations\\UrlAssociations\\http\\UserChoice", false);
                var stringDefault = regDefault.GetValue("ProgId");

                using var regKey = Registry.ClassesRoot.OpenSubKey(stringDefault + "\\shell\\open\\command", false);
                name = regKey.GetValue(null).ToString().ToLower().Replace("\"", "");

                if (!name.EndsWith("exe"))
                    name = name.Substring(0, name.LastIndexOf(".exe") + 4);

            }
            catch
            {
                return string.Empty;
            }

            return name;
        }

        /// <summary> 
        /// Opens search in a new browser. If no browser path is passed in then Chrome is used. 
        /// Leave browser path blank to use Chrome.
        /// </summary>
        public static void OpenInBrowserWindow(this string url, string browserPath = "", bool inPrivate = false, string privateArg = "")
        {
            browserPath = string.IsNullOrEmpty(browserPath) ? GetDefaultBrowserPath() : browserPath;

            var browserExecutableName = browserPath?
                .Split(new[]
                {
                    Path.DirectorySeparatorChar
                }, StringSplitOptions.None)
                .Last();

            var browser = string.IsNullOrEmpty(browserExecutableName) ? "chrome" : browserPath;

            // Internet Explorer will open url in new browser window, and does not take the --new-window parameter
            var browserArguements = (browserExecutableName == "iexplore.exe" ? "" : "--new-window ") + (inPrivate ? $"{privateArg} " : "") + url;

            var psi = new ProcessStartInfo
            {
                FileName = browser,
                Arguments = browserArguements,
                UseShellExecute = true
            };

            try
            {
                Process.Start(psi)?.Dispose();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url, UseShellExecute = true
                });
            }
        }

        /// <summary> 
        /// Opens search as a tab in the default browser chosen in Windows settings.
        /// </summary>
        public static void OpenInBrowserTab(this string url, string browserPath = "", bool inPrivate = false, string privateArg = "")
        {
            browserPath = string.IsNullOrEmpty(browserPath) ? GetDefaultBrowserPath() : browserPath;

            var psi = new ProcessStartInfo()
            {
                UseShellExecute = true
            };
            try
            {
                if (!string.IsNullOrEmpty(browserPath))
                {
                    psi.FileName = browserPath;
                    psi.Arguments = (inPrivate ? $"{privateArg} " : "") + url;
                }
                else
                {
                    psi.FileName = url;
                }

                Process.Start(psi)?.Dispose();
            }
            // This error may be thrown if browser path is incorrect
            catch (System.ComponentModel.Win32Exception)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url, UseShellExecute = true
                });
            }
        }
    }
}