using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Flow.Launcher.Core.ExternalPlugins.Environments;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Stopwatch = Flow.Launcher.Infrastructure.Stopwatch;

namespace Flow.Launcher.Core.Plugin
{
    public static class PluginsLoader
    {
        public static List<PluginPair> Plugins(List<PluginMetadata> metadatas, PluginsSettings settings)
        {
            var dotnetPlugins = DotNetPlugins(metadatas);
            
            var pythonEnv = new PythonEnvironment(metadatas, settings);
            var tsEnv = new TypeScriptEnvironment(metadatas, settings);
            var jsEnv = new JavaScriptEnvironment(metadatas, settings);
            var pythonPlugins = pythonEnv.Setup();
            var tsPlugins = tsEnv.Setup();
            var jsPlugins = jsEnv.Setup();
            
            var executablePlugins = ExecutablePlugins(metadatas);
            
            var plugins = dotnetPlugins
                            .Concat(pythonPlugins)
                            .Concat(tsPlugins)
                            .Concat(jsPlugins)
                            .Concat(executablePlugins)
                            .ToList();
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
                        Assembly assembly = null;
                        IAsyncPlugin plugin = null;

                        try
                        {
                            var assemblyLoader = new PluginAssemblyLoader(metadata.ExecuteFilePath);
                            assembly = assemblyLoader.LoadAssemblyAndDependencies();

                            var type = assemblyLoader.FromAssemblyGetTypeOfInterface(assembly,
                                typeof(IAsyncPlugin));

                            plugin = Activator.CreateInstance(type) as IAsyncPlugin;
                        }
#if DEBUG
                        catch (Exception e)
                        {
                            throw;
                        }
#else
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
#endif

                        if (plugin == null)
                        {
                            erroredPlugins.Add(metadata.Name);
                            return;
                        }

                        plugins.Add(new PluginPair {Plugin = plugin, Metadata = metadata});
                    });
                metadata.InitTime += milliseconds;
            }

            if (erroredPlugins.Count > 0)
            {
                var errorPluginString = String.Join(Environment.NewLine, erroredPlugins);

                var errorMessage = "The following "
                                   + (erroredPlugins.Count > 1 ? "plugins have " : "plugin has ")
                                   + "errored and cannot be loaded:";

                _ = Task.Run(() =>
                {
                    MessageBox.Show($"{errorMessage}{Environment.NewLine}{Environment.NewLine}" +
                                    $"{errorPluginString}{Environment.NewLine}{Environment.NewLine}" +
                                    $"Please refer to the logs for more information", "",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                });
            }

            return plugins;
        }

    public static IEnumerable<PluginPair> ExecutablePlugins(IEnumerable<PluginMetadata> source)
        {
            return source
                .Where(o => o.Language.Equals(AllowedLanguage.Executable, StringComparison.OrdinalIgnoreCase))
                .Select(metadata => new PluginPair
                {
                    Plugin = new ExecutablePlugin(metadata.ExecuteFilePath), Metadata = metadata
                });
        }
    }
}
