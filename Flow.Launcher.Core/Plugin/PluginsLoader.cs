using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Flow.Launcher.Core.ExternalPlugins.Environments;
#pragma warning disable IDE0005
using Flow.Launcher.Infrastructure.Logger;
#pragma warning restore IDE0005
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Stopwatch = Flow.Launcher.Infrastructure.Stopwatch;

namespace Flow.Launcher.Core.Plugin
{
    public static class PluginsLoader
    {
        private static readonly ConcurrentQueue<PluginPair> Plugins = new();

        private static readonly ConcurrentQueue<string> ErroredPlugins = new();

        public static void AddPlugin(PluginPair plugin)
        {
            Plugins.Enqueue(plugin);
        }

        public static async Task<List<PluginPair>> PluginsAsync(List<PluginMetadata> metadatas, PluginsSettings settings)
        {
            var dotnetPlugins = DotNetPlugins(metadatas);

            var pythonEnv = new PythonEnvironment(metadatas, settings);
            var pythonV2Env = new PythonV2Environment(metadatas, settings);
            var tsEnv = new TypeScriptEnvironment(metadatas, settings);
            var jsEnv = new JavaScriptEnvironment(metadatas, settings);
            var tsV2Env = new TypeScriptV2Environment(metadatas, settings);
            var jsV2Env = new JavaScriptV2Environment(metadatas, settings);
            var pythonPlugins = pythonEnv.Setup();
            var pythonV2Plugins = pythonV2Env.Setup();
            var tsPlugins = tsEnv.Setup();
            var jsPlugins = jsEnv.Setup();
            var tsV2Plugins = tsV2Env.Setup();
            var jsV2Plugins = jsV2Env.Setup();

            var executablePlugins = ExecutablePlugins(metadatas);
            var executableV2Plugins = ExecutableV2Plugins(metadatas);

            var plugins = dotnetPlugins
                .Concat(pythonPlugins)
                .Concat(pythonV2Plugins)
                .Concat(tsPlugins)
                .Concat(jsPlugins)
                .Concat(tsV2Plugins)
                .Concat(jsV2Plugins)
                .Concat(executablePlugins)
                .Concat(executableV2Plugins)
                .ToList();

            await Task.WhenAll(plugins);

            if (!ErroredPlugins.IsEmpty)
            {
                var errorPluginString = String.Join(Environment.NewLine, ErroredPlugins);

                var errorMessage = "The following "
                                   + (ErroredPlugins.Count > 1 ? "plugins have " : "plugin has ")
                                   + "errored and cannot be loaded:";

                _ = Task.Run(() =>
                {
                    MessageBox.Show($"{errorMessage}{Environment.NewLine}{Environment.NewLine}" +
                                    $"{errorPluginString}{Environment.NewLine}{Environment.NewLine}" +
                                    $"Please refer to the logs for more information", "",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                });
            }

            return Plugins.ToList();
        }

        public static IEnumerable<Task> DotNetPlugins(List<PluginMetadata> source)
        {
            var metadatas = source.Where(o => AllowedLanguage.IsDotNet(o.Language));

            return metadatas.Select(metadata => Task.Run(() =>
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
                            ErroredPlugins.Enqueue(metadata.Name);
                            return;
                        }

                        Plugins.Enqueue(new PluginPair { Plugin = plugin, Metadata = metadata });
                    });
                metadata.InitTime += milliseconds;
            }));
        }

        public static IEnumerable<Task> ExecutablePlugins(IEnumerable<PluginMetadata> source)
        {
            return source
                .Where(o => o.Language.Equals(AllowedLanguage.Executable, StringComparison.OrdinalIgnoreCase))
                .Select(metadata => Task.Run(() => 
                {
                    Plugins.Enqueue(new PluginPair
                    {
                        Plugin = new ExecutablePlugin(metadata.ExecuteFilePath),
                        Metadata = metadata
                    });
                }));
        }

        public static IEnumerable<Task> ExecutableV2Plugins(IEnumerable<PluginMetadata> source)
        {
            return source
                .Where(o => o.Language.Equals(AllowedLanguage.ExecutableV2, StringComparison.OrdinalIgnoreCase))
                .Select(metadata => Task.Run(() => 
                {
                    Plugins.Enqueue(new PluginPair
                    {
                        Plugin = new ExecutablePluginV2(metadata.ExecuteFilePath),
                        Metadata = metadata
                    });
                }));
        }
    }
}
