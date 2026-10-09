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
    }
}
