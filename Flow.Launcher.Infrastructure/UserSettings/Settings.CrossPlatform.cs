using System.Collections.ObjectModel;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public partial class Settings
    {
        // {clipboard} and {active_explorer_path} rely on WPF clipboard and Windows Explorer; not provided on this platform yet.
        private static partial ObservableCollection<BaseBuiltinShortcutModel> CreateBuiltinShortcuts() => new();

        // The Avalonia app bundles Inter (AppBuilder.WithInterFont), so it is always resolvable.
        public static partial string GetSystemDefaultFont(bool useNoto) => "Inter";
    }
}
