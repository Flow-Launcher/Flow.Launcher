namespace Flow.Launcher.Core.ExternalPlugins.Environments
{
    internal partial class TypeScriptV2Environment
    {
        // Droplex only installs Windows runtimes; here "install" means adopting the system's node.
        internal override void InstallEnvironment()
        {
            var node = FindSystemRuntime("node");
            if (node != null)
            {
                PluginsSettingsFilePath = node;
                API.LogInfo(ClassName, $"Using system Node.js runtime <{node}>");
                return;
            }

            API.ShowMsgError(Localize.failToInstallTypeScriptEnv());
            API.LogError(ClassName, "No node found on this system; install Node.js (e.g. `brew install node`) or select an executable");
        }
    }
}
