using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using System.Windows.Forms;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

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
            var pythonPlugins = PythonPlugins(metadatas, settings.PythonDirectory);
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
                var milliseconds = Stopwatch.Debug(
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
                        IPlugin plugin = null;

                        try
                        {
                            var assemblyLoader = new PluginAssemblyLoader(metadata.ExecuteFilePath);
                            assembly = assemblyLoader.LoadAssemblyAndDependencies();

                            var type = assemblyLoader.FromAssemblyGetTypeOfInterface(assembly, typeof(IPlugin), 
                                typeof(IAsyncPlugin));

                            plugin = Activator.CreateInstance(type);
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

        public static IEnumerable<PluginPair> PythonPlugins(List<PluginMetadata> source, string pythonDirectory)
        {
            // try to set Constant.PythonPath, either from
            // PATH or from the given pythonDirectory
            if (string.IsNullOrEmpty(pythonDirectory))
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
                        Constant.PythonPath = PythonExecutable;
                    }
                    else
                    {
                        Log.Error("|PluginsLoader.PythonPlugins|Python can't be found in PATH.");
                    }
                }
                else
                {
                    Log.Error("|PluginsLoader.PythonPlugins|PATH environment variable is not set.");
                }
            }
            else
            {
                var path = Path.Combine(pythonDirectory, PythonExecutable);
                if (File.Exists(path))
                {
                    Constant.PythonPath = path;
                }
                else
                {
                    Log.Error($"|PluginsLoader.PythonPlugins|Can't find python executable in {path}");
                }
            }

            // if we have a path to the python executable,
            // load every python plugin pair.
            if (String.IsNullOrEmpty(Constant.PythonPath))
            {
                return new List<PluginPair>();
            }
            else
            {
                return source
                    .Where(o => o.Language.ToUpper() == AllowedLanguage.Python)
                    .Select(metadata => new PluginPair
                    {
                        Plugin = new PythonPlugin(Constant.PythonPath),
                        Metadata = metadata
                    });
            }
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