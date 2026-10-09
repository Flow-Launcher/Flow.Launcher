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

        // "finder" is the system file manager sentinel (see AvaloniaPublicAPI.IsSystemFileManager): folders open with
        // `open <dir>` and files are revealed with `open -R <file>`; the arguments document that mapping.
        private static partial List<CustomExplorerViewModel> CreateDefaultCustomExplorerList() => new()
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
