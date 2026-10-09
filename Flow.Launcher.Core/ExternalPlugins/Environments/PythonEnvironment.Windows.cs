using Droplex;
using Flow.Launcher.Plugin.SharedCommands;
using Microsoft.VisualStudio.Threading;

namespace Flow.Launcher.Core.ExternalPlugins.Environments
{
    internal partial class PythonEnvironment
    {
        private JoinableTaskFactory JTF { get; } = new JoinableTaskFactory(new JoinableTaskContext());

        internal override void InstallEnvironment()
        {
            FilesFolders.RemoveFolderIfExists(InstallPath, (s) => API.ShowMsgBox(s));

            // Python 3.11.4 is no longer Windows 7 compatible. If user is on Win 7 and
            // uses Python plugin they need to custom install and use v3.8.9
            JTF.Run(async () =>
            {
                try
                {
                    await DroplexPackage.Drop(App.python_3_11_4_embeddable, InstallPath);

                    PluginsSettingsFilePath = ExecutablePath;
                }
                catch (System.Exception e)
                {
                    API.ShowMsgError(Localize.failToInstallPythonEnv());
                    API.LogException(ClassName, "Failed to install Python environment", e);
                }
            });
        }
    }
}
