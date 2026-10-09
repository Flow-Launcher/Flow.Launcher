namespace Flow.Launcher.Core.ExternalPlugins.Environments
{
    public abstract partial class AbstractPluginEnvironment
    {
        // Core has no toolkit-independent file picker here; behaves as a cancelled dialog (runtime path must be set in settings).
        private static partial string GetFileFromDialog(string title, string filter)
        {
            PublicApi.Instance.LogWarn(ClassName, "Runtime executable file dialog is not supported on this platform");
            return string.Empty;
        }
    }
}
