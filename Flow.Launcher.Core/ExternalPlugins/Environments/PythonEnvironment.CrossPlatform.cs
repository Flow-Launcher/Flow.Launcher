namespace Flow.Launcher.Core.ExternalPlugins.Environments
{
    internal partial class PythonEnvironment
    {
        // Droplex only installs Windows runtimes; a user-selected Python executable is required on this platform.
        internal override void InstallEnvironment()
        {
            API.ShowMsgError(Localize.failToInstallPythonEnv());
            API.LogError(ClassName, "Automatic Python environment installation is not supported on this platform");
        }
    }
}
