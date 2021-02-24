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

        private static readonly AssemblyDependencyResolver referencedPluginPackageDependencyResolver;

        private readonly AssemblyName assemblyName;

        static PluginAssemblyLoader()
        {
            referencedPluginPackageDependencyResolver =
                new AssemblyDependencyResolver(Path.Combine(Constant.ProgramDirectory, "Flow.Launcher.dll"));
        }

        internal PluginAssemblyLoader(string assemblyFilePath)
        {
            dependencyResolver = new AssemblyDependencyResolver(assemblyFilePath);
            assemblyName = new AssemblyName(Path.GetFileNameWithoutExtension(assemblyFilePath));


        }

        internal Assembly LoadAssemblyAndDependencies()
        {
            return LoadFromAssemblyName(assemblyName);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            string assemblyPath = dependencyResolver.ResolveAssemblyToPath(assemblyName);

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
