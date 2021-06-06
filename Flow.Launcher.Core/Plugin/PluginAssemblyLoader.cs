﻿using Flow.Launcher.Infrastructure;
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
        private readonly AssemblyDependencyResolver dependencyResolver;

        private readonly AssemblyName assemblyName;

        private static readonly ConcurrentDictionary<string, byte> loadedAssembly;

        static PluginAssemblyLoader()
        {
            var currentAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            loadedAssembly = new ConcurrentDictionary<string, byte>(
                currentAssemblies.Select(x => new KeyValuePair<string, byte>(x.FullName, default)));

            AppDomain.CurrentDomain.AssemblyLoad += (sender, args) =>
            {
                loadedAssembly[args.LoadedAssembly.FullName] = default;
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
            // Otherwise duplicate assembly will be loaded and some weird behavior will occur, such as WinRT.Runtime.dll
            // will fail due to loading multiple versions in process, each with their own static instance of registration state
            if (assemblyPath == null || ExistsInReferencedPackage(assemblyName))
                return null;

            return LoadFromAssemblyPath(assemblyPath);
        }

        internal Type FromAssemblyGetTypeOfInterface(Assembly assembly, Type type)
        {
            var allTypes = assembly.ExportedTypes;
            return allTypes.First(o => o.IsClass && !o.IsAbstract && o.GetInterfaces().Any(t => t == type));
        }

        internal bool ExistsInReferencedPackage(AssemblyName assemblyName)
        {
            return loadedAssembly.ContainsKey(assemblyName.FullName);
        }
    }
}