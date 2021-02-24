using Flow.Launcher.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Flow.Launcher.Core.Plugin
{
    internal class PluginAssemblyLoader : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver dependencyResolver;

        private readonly AssemblyName assemblyName;

        private static readonly List<Assembly> loadedAssembly;

        static PluginAssemblyLoader()
        {
            loadedAssembly = new List<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
            AppDomain.CurrentDomain.AssemblyLoad += (sender, args) =>
            {
                loadedAssembly.Add(args.LoadedAssembly);
            };
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

            // When resolving dependencies, ignore assembly depenedencies that already exits with Flow.Launcher
            // Otherwise duplicate assembly will be loaded, and some weird behavior will occur such as WinRT.dll
            // will fail to create 

            if (assemblyPath == null || ExistsInReferencedPackage(assemblyName))
                return null;

            return LoadFromAssemblyPath(assemblyPath);
        }

        internal Type FromAssemblyGetTypeOfInterface(Assembly assembly, params Type[] types)
        {
            var allTypes = assembly.ExportedTypes;
            return allTypes.First(o => o.IsClass && !o.IsAbstract && o.GetInterfaces().Intersect(types).Any());
        }

        internal bool ExistsInReferencedPackage(AssemblyName assemblyName)
        {
            if (loadedAssembly.Any(a => a.FullName == assemblyName.FullName))
                return true;
            return false;
        }
    }
}