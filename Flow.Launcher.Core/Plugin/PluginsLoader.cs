using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.ExternalPlugins.Environments;
#pragma warning disable IDE0005
using Flow.Launcher.Infrastructure.Logger;
#pragma warning restore IDE0005
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin
{
    public static class PluginsLoader
    {
        private static readonly string ClassName = nameof(PluginsLoader);

        // We should not initialize API in static constructor because it will create another API instance
        private static IPublicAPI api = null;
        private static IPublicAPI API => api ??= Ioc.Default.GetRequiredService<IPublicAPI>();

        public static List<PluginPair> Plugins(List<PluginMetadata> metadatas, PluginsSettings settings)
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
            return plugins;
        }

        private static IEnumerable<PluginPair> DotNetPlugins(List<PluginMetadata> source)
        {
            var erroredPlugins = new List<string>();

            var plugins = new List<PluginPair>();
            var metadatas = source.Where(o => AllowedLanguage.IsDotNet(o.Language));

            foreach (var metadata in metadatas)
            {
                var milliseconds = API.StopwatchLogDebug(ClassName, $"Constructor init cost for {metadata.Name}", () =>
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

                            metadata.AssemblyName = assembly.GetName().Name;
                        }
#if DEBUG
                        catch (Exception)
                        {
                            throw;
                        }
#else
                        catch (Exception e) when (assembly == null)
                        {
                            Log.Exception(ClassName, $"Couldn't load assembly for the plugin: {metadata.Name}", e);
                        }
                        catch (InvalidOperationException e)
                        {
                            Log.Exception(ClassName, $"Can't find the required IPlugin interface for the plugin: <{metadata.Name}>", e);
                        }
                        catch (ReflectionTypeLoadException e)
                        {
                            Log.Exception(ClassName, $"The GetTypes method was unable to load assembly types for the plugin: <{metadata.Name}>", e);
                        }
                        catch (Exception e)
                        {
                            Log.Exception(ClassName, $"The following plugin has errored and can not be loaded: <{metadata.Name}>", e);
                        }
#endif

                        if (plugin == null)
                        {
                            erroredPlugins.Add(metadata.Name);
                            return;
                        }

                        plugins.Add(new PluginPair { Plugin = plugin, Metadata = metadata });
                    });
                metadata.InitTime += milliseconds;
            }

            if (erroredPlugins.Count > 0)
            {
                var errorPluginString = string.Join(Environment.NewLine, erroredPlugins);

                var errorMessage = erroredPlugins.Count > 1 ?
                    API.GetTranslation("pluginsHaveErrored") :
                    API.GetTranslation("pluginHasErrored");

                API.ShowMsgError($"{errorMessage}{Environment.NewLine}{Environment.NewLine}" +
                    $"{errorPluginString}{Environment.NewLine}{Environment.NewLine}" +
                    API.GetTranslation("referToLogs"));
            }

            return plugins;
        }

        private static IEnumerable<PluginPair> ExecutablePlugins(IEnumerable<PluginMetadata> source)
        {
            return source
                .Where(o => o.Language.Equals(AllowedLanguage.Executable, StringComparison.OrdinalIgnoreCase))
                .Select(metadata =>
                {
                    return new PluginPair
                    {
                        Plugin = new ExecutablePlugin(metadata.ExecuteFilePath),
                        Metadata = metadata
                    };
                });
        }

        private static IEnumerable<PluginPair> ExecutableV2Plugins(IEnumerable<PluginMetadata> source)
        {
            return source
                .Where(o => o.Language.Equals(AllowedLanguage.ExecutableV2, StringComparison.OrdinalIgnoreCase))
                .Select(metadata =>
                {
                    return new PluginPair
                    {
                        Plugin = new ExecutablePlugin(metadata.ExecuteFilePath),
                        Metadata = metadata
                    };
                });
        }
    }
}
