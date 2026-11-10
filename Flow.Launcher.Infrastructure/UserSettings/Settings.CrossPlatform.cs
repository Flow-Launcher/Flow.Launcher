using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public partial class Settings
    {
        // {clipboard} and {active_explorer_path} rely on WPF clipboard and Windows Explorer; not provided on this platform yet.
        private static partial ObservableCollection<BaseBuiltinShortcutModel> CreateBuiltinShortcuts() => new();

        // The Avalonia app bundles Inter (AppBuilder.WithInterFont), so it is always resolvable.
        public static partial string GetSystemDefaultFont(bool useNoto) => "Inter";

        // "finder" (macOS) and "xdg-open" (Linux) are the system file manager sentinels (see
        // AvaloniaPublicAPI.IsSystemFileManager). macOS opens folders with `open <dir>` and reveals files with
        // `open -R <file>`; Linux opens folders with xdg-open and reveals files through org.freedesktop.FileManager1.
        // The arguments document that mapping.
        private static partial List<CustomExplorerViewModel> CreateDefaultCustomExplorerList() => System.OperatingSystem.IsLinux()
            ? new()
            {
                new()
                {
                    Name = "File Manager",
                    Path = "xdg-open",
                    DirectoryArgument = "\"%d\"",
                    FileArgument = "\"%f\"",
                    Editable = false
                }
            }
            : new()
            {
                new()
                {
                    Name = "Finder",
                    Path = "finder",
                    DirectoryArgument = "\"%d\"",
                    FileArgument = "-R \"%f\"",
                    Editable = false
                }
            };
    }
}
