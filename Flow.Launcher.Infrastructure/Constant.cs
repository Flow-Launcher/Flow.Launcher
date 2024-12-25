using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Flow.Launcher.Infrastructure
{
    public static class Constant
    {
        public const string FlowLauncher = "Flow.Launcher";
        public const string FlowLauncherFullName = "Flow Launcher";
        public const string Plugins = "Plugins";
        public const string PluginMetadataFileName = "plugin.json";

        public const string ApplicationFileName = FlowLauncher + ".exe";

        private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
        public static readonly string ProgramDirectory = Directory.GetParent(Assembly.Location.NonNull()).ToString();
        public static readonly string ExecutablePath = Path.Combine(ProgramDirectory, FlowLauncher + ".exe");
        public static readonly string ApplicationDirectory = Directory.GetParent(ProgramDirectory).ToString();
        public static readonly string RootDirectory = Directory.GetParent(ApplicationDirectory).ToString();
        
        public static readonly string PreinstalledDirectory = Path.Combine(ProgramDirectory, Plugins);
        public const string IssuesUrl = "https://github.com/Flow-Launcher/Flow.Launcher/issues";
        public static readonly string Version = FileVersionInfo.GetVersionInfo(Assembly.Location.NonNull()).ProductVersion;
        public static readonly string Dev = "Dev";
        public const string Documentation = "https://flowlauncher.com/docs/#/usage-tips";

        public static readonly int ThumbnailSize = 64;
        private static readonly string ImagesDirectory = Path.Combine(ProgramDirectory, "Images");
        public static readonly string DefaultIcon = Path.Combine(ImagesDirectory, "app.png");
        public static readonly string ErrorIcon = Path.Combine(ImagesDirectory, "app_error.png");
        public static readonly string MissingImgIcon = Path.Combine(ImagesDirectory, "app_missing_img.png");
        public static readonly string LoadingImgIcon = Path.Combine(ImagesDirectory, "loading.png");
        public static readonly string ImageIcon = Path.Combine(ImagesDirectory, "image.png");

        public static string PythonPath;
        public static string NodePath;

        public static readonly string QueryTextBoxIconImagePath = $"{ProgramDirectory}\\Images\\mainsearch.svg";

        public const string DefaultTheme = "Win11Light";

        public const string Light = "Light";
        public const string Dark = "Dark";
        public const string System = "System";

        public const string Themes = "Themes";
        public const string Settings = "Settings";
        public const string Logs = "Logs";

        public const string Website = "https://flowlauncher.com";
        public const string SponsorPage = "https://github.com/sponsors/Flow-Launcher";
        public const string GitHub = "https://github.com/Flow-Launcher/Flow.Launcher";
        public const string Docs = "https://flowlauncher.com/docs";
    }
}
