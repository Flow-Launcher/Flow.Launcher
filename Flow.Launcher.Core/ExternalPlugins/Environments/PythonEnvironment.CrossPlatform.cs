using System.IO;

namespace Flow.Launcher.Core.ExternalPlugins.Environments
{
    internal partial class PythonEnvironment
    {
        // /usr/bin/python3 is a stub that prompts to install the Command Line Tools unless they (or Xcode) provide Python.
        private const string AppleStubPython = "/usr/bin/python3";
        private static readonly string[] ApplePythonLocations =
        [
            "/Library/Developer/CommandLineTools/usr/bin/python3",
            "/Applications/Xcode.app/Contents/Developer/usr/bin/python3"
        ];

        // Droplex only installs Windows runtimes; here "install" means adopting the system's python3.
        internal override void InstallEnvironment()
        {
            var python = FindSystemRuntime("python3");
            if (python == AppleStubPython && !System.Array.Exists(ApplePythonLocations, File.Exists))
            {
                python = null;
            }

            if (python != null)
            {
                PluginsSettingsFilePath = python;
                API.LogInfo(ClassName, $"Using system Python runtime <{python}>");
                return;
            }

            API.ShowMsgError(Localize.failToInstallPythonEnv());
            API.LogError(ClassName, "No python3 found on this system; install Python 3 (e.g. `brew install python`) or select an executable");
        }
    }
}
