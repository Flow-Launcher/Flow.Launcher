using Flow.Launcher.Infrastructure;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Flow.Launcher.Core.Plugin
{
    internal class PluginAssemblyLoader : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _dependencyResolver;

        private readonly AssemblyName _assemblyName;

        internal PluginAssemblyLoader(string assemblyFilePath)
        {
            _dependencyResolver = new AssemblyDependencyResolver(assemblyFilePath);
            _assemblyName = new AssemblyName(Path.GetFileNameWithoutExtension(assemblyFilePath));
        }

        internal Assembly LoadAssemblyAndDependencies()
        {
            return LoadFromAssemblyName(_assemblyName);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            string assemblyPath = _dependencyResolver.ResolveAssemblyToPath(assemblyName);

            // When resolving dependencies, ignore assembly depenedencies that already exits with Flow.Launcher
            // Otherwise duplicate assembly will be loaded and some weird behavior will occur, such as WinRT.Runtime.dll
            // will fail due to loading multiple versions in process, each with their own static instance of registration state
            var existAssembly = Default.Assemblies.FirstOrDefault(x => x.FullName == assemblyName.FullName);

            return existAssembly ?? (assemblyPath == null ? null : LoadFromAssemblyPath(assemblyPath));
        }

        internal Type FromAssemblyGetTypeOfInterface(Assembly assembly, Type type)
        {
            var allTypes = assembly.ExportedTypes;
            return allTypes.First(o => o.IsClass && !o.IsAbstract && o.GetInterfaces().Any(t => t == type));
        }
    }
}