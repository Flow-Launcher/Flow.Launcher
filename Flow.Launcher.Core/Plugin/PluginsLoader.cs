using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        private static List<PluginPair> DotNetPlugins(List<PluginMetadata> source)
        {
            var erroredPlugins = new List<string>();

            var plugins = new List<PluginPair>();
            var metadatas = source.Where(o => AllowedLanguage.IsDotNet(o.Language));

            foreach (var metadata in metadatas)
            {
                var pair = DotNetPlugin(metadata);
                if (pair == null)
                {
                    erroredPlugins.Add(metadata.Name);
                }
                else
                {
                    plugins.Add(pair);
                }
            }

            if (erroredPlugins.Count > 0)
            {
                var errorPluginString = string.Join(Environment.NewLine, erroredPlugins);

                var errorMessage = erroredPlugins.Count > 1 ?
                    Localize.pluginsHaveErrored():
                    Localize.pluginHasErrored();

                PublicApi.Instance.ShowMsgError($"{errorMessage}{Environment.NewLine}{Environment.NewLine}" +
                    $"{errorPluginString}{Environment.NewLine}{Environment.NewLine}" +
                    Localize.referToLogs());
            }

            return plugins;
        }

        internal static PluginPair DotNetPlugin(PluginMetadata metadata)
        {
            PluginPair pair = null;

            var milliseconds = PublicApi.Instance.StopwatchLogDebug(ClassName, $"Constructor init cost for {metadata.Name}", () =>
            {
                Assembly assembly = null;
                IAsyncPlugin plugin = null;
                PluginAssemblyLoader assemblyLoader = null;

                try
                {
                    assemblyLoader = new PluginAssemblyLoader(metadata.ExecuteFilePath);
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
                    PublicApi.Instance.LogException(ClassName, $"Couldn't load assembly for the plugin: {metadata.Name}", e);
                }
                catch (InvalidOperationException e)
                {
                    PublicApi.Instance.LogException(ClassName, $"Can't find the required IPlugin interface for the plugin: <{metadata.Name}>", e);
                }
                catch (ReflectionTypeLoadException e)
                {
                    PublicApi.Instance.LogException(ClassName, $"The GetTypes method was unable to load assembly types for the plugin: <{metadata.Name}>", e);
                }
                catch (Exception e)
                {
                    PublicApi.Instance.LogException(ClassName, $"The following plugin has errored and can not be loaded: <{metadata.Name}>", e);
                }
#endif

                if (plugin == null) return;

                // Track the load context only for an accepted plugin, so a rejected one cannot
                // displace the running instance's context; it is used to unload the assembly when
                // the plugin is reloaded or uninstalled
                PluginManager.TrackAssemblyLoader(metadata.ID, assemblyLoader);

                pair = new PluginPair { Plugin = plugin, Metadata = metadata };
            });

            metadata.InitTime += milliseconds;
            PublicApi.Instance.LogDebug(ClassName, $"Constructor cost for <{metadata.Name}> is <{metadata.InitTime}ms>");

            return pair;
        }

        /// <summary>
        /// Loads a single plugin from its already-parsed metadata, dispatching on the plugin language.
        /// Used by hot reload; the batch equivalent for startup is <see cref="Plugins"/>.
        /// </summary>
        internal static PluginPair LoadPlugin(PluginMetadata metadata, PluginsSettings settings)
        {
            if (AllowedLanguage.IsDotNet(metadata.Language))
            {
                return DotNetPlugin(metadata);
            }

            var metadatas = new List<PluginMetadata> { metadata };

            if (AllowedLanguage.IsPython(metadata.Language) || AllowedLanguage.IsNodeJs(metadata.Language))
            {
                AbstractPluginEnvironment environment = metadata.Language.ToUpperInvariant() switch
                {
                    "PYTHON" => new PythonEnvironment(metadatas, settings),
                    "PYTHON_V2" => new PythonV2Environment(metadatas, settings),
                    "TYPESCRIPT" => new TypeScriptEnvironment(metadatas, settings),
                    "TYPESCRIPT_V2" => new TypeScriptV2Environment(metadatas, settings),
                    "JAVASCRIPT" => new JavaScriptEnvironment(metadatas, settings),
                    _ => new JavaScriptV2Environment(metadatas, settings),
                };
                return environment.Setup().FirstOrDefault();
            }

            if (AllowedLanguage.IsExecutable(metadata.Language))
            {
                return ExecutablePlugins(metadatas).Concat(ExecutableV2Plugins(metadatas)).FirstOrDefault();
            }

            PublicApi.Instance.LogError(ClassName, $"Unsupported language <{metadata.Language}> for plugin <{metadata.Name}>");
            return null;
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
