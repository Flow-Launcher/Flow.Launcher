using Droplex;
using Flow.Launcher.Plugin.SharedCommands;
using Microsoft.VisualStudio.Threading;

namespace Flow.Launcher.Core.ExternalPlugins.Environments
{
    internal partial class TypeScriptEnvironment
    {
        private JoinableTaskFactory JTF { get; } = new JoinableTaskFactory(new JoinableTaskContext());

        internal override void InstallEnvironment()
        {
            FilesFolders.RemoveFolderIfExists(InstallPath, (s) => API.ShowMsgBox(s));

            JTF.Run(async () =>
            {
                try
                {
                    await DroplexPackage.Drop(App.nodejs_16_18_0, InstallPath);

                    PluginsSettingsFilePath = ExecutablePath;
                }
                catch (System.Exception e)
                {
                    API.ShowMsgError(Localize.failToInstallTypeScriptEnv());
                    API.LogException(ClassName, "Failed to install TypeScript environment", e);
                }
            });
        }
    }
}
