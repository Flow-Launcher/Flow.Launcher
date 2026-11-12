using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public partial class Settings
    {
        partial void InitializeSettingWindowFontResources()
        {
            var settingWindowFont = new FontFamily(SettingWindowFont);
            Application.Current.Resources["SettingWindowFont"] = settingWindowFont;
            Application.Current.Resources["ContentControlThemeFontFamily"] = settingWindowFont;
        }

        partial void UpdateSettingWindowFontResources(string font)
        {
            if (Application.Current != null)
            {
                Application.Current.Resources["SettingWindowFont"] = new FontFamily(font);
                Application.Current.Resources["ContentControlThemeFontFamily"] = new FontFamily(font);
            }
        }

        private static partial ObservableCollection<BaseBuiltinShortcutModel> CreateBuiltinShortcuts() => new()
        {
            new AsyncBuiltinShortcutModel("{clipboard}", "shortcut_clipboard_description", () => Win32Helper.StartSTATaskAsync(Clipboard.GetText)),
            new BuiltinShortcutModel("{active_explorer_path}", "shortcut_active_explorer_path", FileExplorerHelper.GetActiveExplorerPath)
        };

        public static partial string GetSystemDefaultFont(bool useNoto) => Win32Helper.GetSystemDefaultFont(useNoto);

        private static partial List<CustomExplorerViewModel> CreateDefaultCustomExplorerList() => new()
        {
            new()
            {
                Name = "Explorer",
                Path = "explorer",
                DirectoryArgument = "\"%d\"",
                FileArgument = "/select, \"%f\"",
                Editable = false
            },
            new()
            {
                Name = "Total Commander",
                Path = @"C:\Program Files\totalcmd\TOTALCMD64.exe",
                DirectoryArgument = "/O /A /S /T \"%d\"",
                FileArgument = "/O /A /S /T \"%f\""
            },
            new()
            {
                Name = "Directory Opus",
                Path = @"C:\Program Files\GPSoftware\Directory Opus\dopusrt.exe",
                DirectoryArgument = "/cmd Go \"%d\" NEW",
                FileArgument = "/cmd Go \"%f\" NEW"

            },
            new()
            {
                Name = "Files",
                Path = "Files-Stable",
                DirectoryArgument = "\"%d\"",
                FileArgument = "-select \"%f\""
            }
        };
    }
}
