using Flow.Launcher.Infrastructure;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Flow.Launcher.Core.Plugin
{
    internal class PluginAssemblyLoader : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver dependencyResolver;

        private readonly AssemblyDependencyResolver referencedPluginPackageDependencyResolver;

        private readonly AssemblyName assemblyName;

        internal PluginAssemblyLoader(string assemblyFilePath)
        {
            dependencyResolver = new AssemblyDependencyResolver(assemblyFilePath);
            assemblyName = new AssemblyName(Path.GetFileNameWithoutExtension(assemblyFilePath));

            referencedPluginPackageDependencyResolver =
                new AssemblyDependencyResolver(Path.Combine(Constant.ProgramDirectory, "Flow.Launcher.Plugin.dll"));
        }

        internal Assembly LoadAssemblyAndDependencies()
        {
            return LoadFromAssemblyName(assemblyName);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            string assemblyPath = dependencyResolver.ResolveAssemblyToPath(assemblyName);

            // When resolving dependencies, ignore assembly depenedencies that already exits with Flow.Launcher.Plugin
            // Otherwise will get unexpected behaviour with plugins, e.g. JsonIgnore attribute not honored in WebSearch or other plugins
            // that use Newtonsoft.Json
            if (assemblyPath == null || ExistsInReferencedPluginPackage(assemblyName))
                return null;

            return LoadFromAssemblyPath(assemblyPath);
        }

        internal Type FromAssemblyGetTypeOfInterface(Assembly assembly, params Type[] types)
        {
            var allTypes = assembly.ExportedTypes;

            return allTypes.First(o => o.IsClass && !o.IsAbstract && o.GetInterfaces().Intersect(types).Any());
        }

        internal bool ExistsInReferencedPluginPackage(AssemblyName assemblyName)
        {
            return referencedPluginPackageDependencyResolver.ResolveAssemblyToPath(assemblyName) != null;
        }
    }
}