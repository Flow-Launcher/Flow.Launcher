using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Droplex;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Core.Plugin
{
    public static class PluginsLoader
    {
        public const string PATH = "PATH";
        public const string Python = "python";
        public const string PythonExecutable = "pythonw.exe";

        public static List<PluginPair> Plugins(List<PluginMetadata> metadatas, PluginsSettings settings)
        {
            var dotnetPlugins = DotNetPlugins(metadatas);
            var pythonPlugins = PythonPlugins(metadatas, settings);
            var executablePlugins = ExecutablePlugins(metadatas);
            var plugins = dotnetPlugins.Concat(pythonPlugins).Concat(executablePlugins).ToList();
            return plugins;
        }

        public static IEnumerable<PluginPair> DotNetPlugins(List<PluginMetadata> source)
        {
            var erroredPlugins = new List<string>();

            var plugins = new List<PluginPair>();
            var metadatas = source.Where(o => AllowedLanguage.IsDotNet(o.Language));

            foreach (var metadata in metadatas)
            {
                var milliseconds = Stopwatch.Normal(
                    $"|PluginsLoader.DotNetPlugins|Constructor init cost for {metadata.Name}", () =>
                    {
#if DEBUG
                        var assemblyLoader = new PluginAssemblyLoader(metadata.ExecuteFilePath);
                        var assembly = assemblyLoader.LoadAssemblyAndDependencies();
                        var type = assemblyLoader.FromAssemblyGetTypeOfInterface(assembly, typeof(IPlugin),
                            typeof(IAsyncPlugin));

                        var plugin = Activator.CreateInstance(type);
#else
                        Assembly assembly = null;
                        object plugin = null;

                        try
                        {
                            PluginAssemblyLoader assemblyLoader = null;
                            Stopwatch.Normal($"|PluginsLoader.DotNetPlugins|Assembly Constructor cost for {metadata.Name}",
                                delegate
                                {
                                    assemblyLoader = new PluginAssemblyLoader(metadata.ExecuteFilePath);
                                    assembly = assemblyLoader.LoadAssemblyAndDependencies();
                                });

                            Stopwatch.Normal($"|PluginsLoader.DotNetPlugins|Constructor init cost for {metadata.Name}",
                                delegate
                                {
                                    var type = assemblyLoader.FromAssemblyGetTypeOfInterface(assembly, typeof(IPlugin),
                                                                                                       typeof(IAsyncPlugin));
                                        
                                    plugin = Activator.CreateInstance(type);
                                });
                        }
                        catch (Exception e) when (assembly == null)
                        {
                            Log.Exception($"|PluginsLoader.DotNetPlugins|Couldn't load assembly for the plugin: {metadata.Name}", e);
                        }
                        catch (InvalidOperationException e)
                        {
                            Log.Exception($"|PluginsLoader.DotNetPlugins|Can't find the required IPlugin interface for the plugin: <{metadata.Name}>", e);
                        }
                        catch (ReflectionTypeLoadException e)
                        {
                            Log.Exception($"|PluginsLoader.DotNetPlugins|The GetTypes method was unable to load assembly types for the plugin: <{metadata.Name}>", e);
                        }
                        catch (Exception e)
                        {
                            Log.Exception($"|PluginsLoader.DotNetPlugins|The following plugin has errored and can not be loaded: <{metadata.Name}>", e);
                        }

                        if (plugin == null)
                        {
                            erroredPlugins.Add(metadata.Name);
                            return;
                        }
#endif
                        plugins.Add(new PluginPair
                        {
                            Plugin = plugin,
                            Metadata = metadata
                        });
                    });
                metadata.InitTime += milliseconds;
            }

            if (erroredPlugins.Count > 0)
            {
                var errorPluginString = String.Join(Environment.NewLine, erroredPlugins);

                var errorMessage = "The following "
                                   + (erroredPlugins.Count > 1 ? "plugins have " : "plugin has ")
                                   + "errored and cannot be loaded:";

                Task.Run(() =>
                {
                    MessageBox.Show($"{errorMessage}{Environment.NewLine}{Environment.NewLine}" +
                                    $"{errorPluginString}{Environment.NewLine}{Environment.NewLine}" +
                                    $"Please refer to the logs for more information", "",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                });
            }

            return plugins;
        }

        public static IEnumerable<PluginPair> PythonPlugins(List<PluginMetadata> source, PluginsSettings settings)
        {
            if (!source.Any(o => o.Language.ToUpper() == AllowedLanguage.Python))
                return new List<PluginPair>();

            // Try setting Constant.PythonPath first, either from
            // PATH or from the given pythonDirectory
            if (string.IsNullOrEmpty(settings.PythonDirectory))
            {
                var paths = Environment.GetEnvironmentVariable(PATH);
                if (paths != null)
                {
                    var pythonInPath = paths
                        .Split(';')
                        .Where(p => p.ToLower().Contains(Python))
                        .Any();

                    if (pythonInPath)
                    {
                        Constant.PythonPath =
                            Path.Combine(paths.Split(';').Where(p => p.ToLower().Contains(Python)).FirstOrDefault(), PythonExecutable);
                        settings.PythonDirectory = FilesFolders.GetPreviousExistingDirectory(FilesFolders.LocationExists, Constant.PythonPath);
                    }
                    else
                    {
                        Log.Error("PluginsLoader", "Failed to set Python path despite the environment variable PATH is found", "PythonPlugins");
                    }
                }
            }
            else
            {
                var path = Path.Combine(settings.PythonDirectory, PythonExecutable);
                if (File.Exists(path))
                {
                    Constant.PythonPath = path;
                }
                else
                {
                    Log.Error("PluginsLoader", $"Tried to automatically set from Settings.PythonDirectory " +
                        $"but can't find python executable in {path}", "PythonPlugins");
                }
            }

            if (string.IsNullOrEmpty(settings.PythonDirectory))
            {
                if (MessageBox.Show("Flow detected you have installed Python plugins, " +
                        "would you like to install Python to run them? " +
                        Environment.NewLine + Environment.NewLine +
                        "Click no if it's already installed, " +
                        "and you will be prompted to select the folder that contains the Python executable",
                        string.Empty, MessageBoxButtons.YesNo) == DialogResult.No
                    && string.IsNullOrEmpty(settings.PythonDirectory))
                {
                    var dlg = new FolderBrowserDialog
                    {
                        SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
                    };

                    var result = dlg.ShowDialog();
                    if (result == DialogResult.OK)
                    {
                        string pythonDirectory = dlg.SelectedPath;
                        if (!string.IsNullOrEmpty(pythonDirectory))
                        {
                            var pythonPath = Path.Combine(pythonDirectory, PythonExecutable);
                            if (File.Exists(pythonPath))
                            {
                                settings.PythonDirectory = pythonDirectory;
                                Constant.PythonPath = pythonPath;
                            }
                            else
                            {
                                MessageBox.Show("Can't find python in given directory");
                            }
                        }
                    }
                }
                else
                {
                    DroplexPackage.Drop(App.python3_9_1).Wait();

                    var installedPythonDirectory =
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python39");
                    var pythonPath = Path.Combine(installedPythonDirectory, PythonExecutable);
                    if (FilesFolders.FileExists(pythonPath))
                    {
                        settings.PythonDirectory = installedPythonDirectory;
                        Constant.PythonPath = pythonPath;
                    }
                    else
                    {
                        Log.Error("PluginsLoader",
                            $"Failed to set Python path after Droplex install, {pythonPath} does not exist",
                            "PythonPlugins");
                    }
                }
            }

            if (string.IsNullOrEmpty(settings.PythonDirectory))
            {
                MessageBox.Show("Unable to set Python executable path, please try from Flow's settings (scroll down to the bottom).");
                Log.Error("PluginsLoader",
                    $"Not able to successfully set Python path, the PythonDirectory variable is still an empty string.",
                    "PythonPlugins");

                return new List<PluginPair>();
            }

            return source
                .Where(o => o.Language.ToUpper() == AllowedLanguage.Python)
                .Select(metadata => new PluginPair
                {
                    Plugin = new PythonPlugin(Constant.PythonPath),
                    Metadata = metadata
                })
                .ToList();
        }

        public static IEnumerable<PluginPair> ExecutablePlugins(IEnumerable<PluginMetadata> source)
        {
            return source
                .Where(o => o.Language.ToUpper() == AllowedLanguage.Executable)
                .Select(metadata => new PluginPair
                {
                    Plugin = new ExecutablePlugin(metadata.ExecuteFilePath),
                    Metadata = metadata
                });
        }
    }
}
