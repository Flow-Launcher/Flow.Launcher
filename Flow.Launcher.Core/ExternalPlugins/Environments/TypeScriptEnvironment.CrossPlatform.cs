namespace Flow.Launcher.Core.ExternalPlugins.Environments
{
    internal partial class TypeScriptEnvironment
    {
        // Droplex only installs Windows runtimes; a user-selected Node.js executable is required on this platform.
        internal override void InstallEnvironment()
        {
            API.ShowMsgError(Localize.failToInstallTypeScriptEnv());
            API.LogError(ClassName, "Automatic TypeScript environment installation is not supported on this platform");
        }
    }
}
