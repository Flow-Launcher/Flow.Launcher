using Droplex;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Flow.Launcher.Core.ExternalPlugins
{
    public class PluginEnvironment
    {
        private const string PythonExecutable = "pythonw.exe";
        
        private const string NodeExecutable = "node.exe";

        private const string PythonEnv = "Python";

        private const string NodeEnv = "Node.js";

        private List<PluginMetadata> pluginMetadataList;

        private PluginsSettings pluginSettings;

        internal PluginEnvironment(List<PluginMetadata> pluginMetadataList, PluginsSettings pluginSettings)
        {
            this.pluginMetadataList = pluginMetadataList;
            this.pluginSettings = pluginSettings;
        }
        //TODO: CHECK IF NEED TO RESET PATH AFTER FLOW UPDATE
        // LOG NODE PATH
        internal IEnumerable<PluginPair> PythonSetup()
        {
            return Setup(AllowedLanguage.Python, PythonEnv);
        }

        internal IEnumerable<PluginPair> TypeScriptSetup()
        {
            return Setup(AllowedLanguage.TypeScript, NodeEnv);
        }

        internal IEnumerable<PluginPair> JavaScriptSetup()
        {
            return Setup(AllowedLanguage.JavaScript, NodeEnv);
        }

        private IEnumerable<PluginPair> Setup(string languageType, string environment)
        {
            if (!pluginMetadataList.Any(o => o.Language.Equals(languageType, StringComparison.OrdinalIgnoreCase)))
                return new List<PluginPair>();

            var envFilePath = string.Empty;

            switch (languageType)
            {
                case AllowedLanguage.Python:
                    if (!string.IsNullOrEmpty(pluginSettings.PythonDirectory) && FilesFolders.LocationExists(pluginSettings.PythonDirectory))
                        return SetPathForPluginPairs($"{pluginSettings.PythonDirectory}\\{PythonExecutable}", languageType);
                    break;

                case AllowedLanguage.TypeScript:
                case AllowedLanguage.JavaScript:
                    if (!string.IsNullOrEmpty(pluginSettings.NodeFilePath) && FilesFolders.FileExists(pluginSettings.NodeFilePath))
                        return SetPathForPluginPairs(pluginSettings.NodeFilePath, languageType);
                    break;

                default:
                    break;
            }

            if (MessageBox.Show($"Flow detected you have installed {languageType} plugins, which " +
                                $"will require {environment} to run. Would you like to download {environment}? " +
                                Environment.NewLine + Environment.NewLine +
                                "Click no if it's already installed, " +
                                $"and you will be prompted to select the folder that contains the {environment} executable",
                    string.Empty, MessageBoxButtons.YesNo) == DialogResult.No)
            {
                var dlg = new OpenFileDialog
                {
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Multiselect = false,
                    CheckFileExists = true,
                    CheckPathExists = true,
                    Title = $"Please select the {environment} executable"
                };

                var result = dlg.ShowDialog();
                if (result == DialogResult.OK)
                {
                    switch (languageType)
                    {
                        case AllowedLanguage.Python:
                            Constant.PythonPath = dlg.FileName;
                            pluginSettings.PythonDirectory = Constant.PythonPath;
                            break;
                        case AllowedLanguage.TypeScript:
                        case AllowedLanguage.JavaScript:
                            Constant.NodePath = dlg.FileName;
                            pluginSettings.NodeFilePath = Constant.NodePath;
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    InstallEnvironment(languageType);
                }
            }
            else
            {
                InstallEnvironment(languageType);
            }

            switch (languageType)
            {
                case AllowedLanguage.Python when FilesFolders.FileExists(Constant.PythonPath) && !string.IsNullOrEmpty(pluginSettings.PythonDirectory):
                    return SetPathForPluginPairs(Constant.PythonPath, languageType);

                case AllowedLanguage.TypeScript when FilesFolders.FileExists(Constant.NodePath) && !string.IsNullOrEmpty(pluginSettings.NodeFilePath):
                case AllowedLanguage.JavaScript when FilesFolders.FileExists(Constant.NodePath) && !string.IsNullOrEmpty(pluginSettings.NodeFilePath):
                    return SetPathForPluginPairs(pluginSettings.NodeFilePath, languageType);

                default:
                    MessageBox.Show(
                    "Unable to set Python executable path, please try from Flow's settings (scroll down to the bottom).");
                    Log.Error("PluginsLoader",
                        $"Not able to successfully set Python path, the PythonDirectory variable is still an empty string.",
                        "PythonPlugins");

                    return new List<PluginPair>();
            }
        }

        private void InstallEnvironment(string languageType)
        {
            switch (languageType)
            {
                case AllowedLanguage.Python:
                    var pythonDirPath = Path.Combine(DataLocation.DataDirectory(), "PythonEmbeddable");
                    FilesFolders.RemoveFolderIfExists(pythonDirPath);

                    // Python 3.8.9 is used for Windows 7 compatibility
                    DroplexPackage.Drop(App.python_3_8_9_embeddable, pythonDirPath).Wait();

                    pluginSettings.PythonDirectory = pythonDirPath;
                    Constant.PythonPath = Path.Combine(pythonDirPath, PythonExecutable);

                    break;

                case AllowedLanguage.TypeScript:
                case AllowedLanguage.JavaScript:
                    var nodeDirPath = Path.Combine(DataLocation.DataDirectory(), "Node");
                    FilesFolders.RemoveFolderIfExists(nodeDirPath);

                    DroplexPackage.Drop(App.nodejs_16_18_0, nodeDirPath).Wait();

                    Constant.NodePath = Path.Combine(nodeDirPath, $"node-v16.18.0-win-x64\\{NodeExecutable}");
                    pluginSettings.NodeFilePath = Constant.NodePath;

                    break;

                default:
                    break;
            }
        }

        private IEnumerable<PluginPair> SetPathForPluginPairs(string filePath, string languageToSet)
        {
            var pluginPairs = new List<PluginPair>();

            foreach (var metadata in pluginMetadataList) {

                switch (languageToSet)
                {
                    case AllowedLanguage.Python when metadata.Language.Equals(languageToSet, StringComparison.OrdinalIgnoreCase):
                        pluginPairs.Add(new PluginPair
                        {
                            Plugin = new PythonPlugin(filePath),
                            Metadata = metadata
                        });
                        break;
                    
                    case AllowedLanguage.TypeScript when metadata.Language.Equals(languageToSet, StringComparison.OrdinalIgnoreCase):
                        pluginPairs.Add(new PluginPair
                        {
                            Plugin = new NodePlugin(filePath),
                            Metadata = metadata
                        });
                        break;

                    case AllowedLanguage.JavaScript when metadata.Language.Equals(languageToSet, StringComparison.OrdinalIgnoreCase):
                        pluginPairs.Add(new PluginPair
                        {
                            Plugin = new NodePlugin(filePath),
                            Metadata = metadata
                        });
                        break;

                    default:
                        break;
                }
            }

            return pluginPairs;
        }

        public static string GetFileFromDialog(string title, string filter="")
        {
            var dlg = new OpenFileDialog
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Multiselect = false,
                CheckFileExists = true,
                CheckPathExists = true,
                Title = title,
                Filter = filter
            };

            var result = dlg.ShowDialog();
            if (result == DialogResult.OK)
            {
                return dlg.FileName;
            }
            else
            {
                return string.Empty;
            }
        }

    }
}
